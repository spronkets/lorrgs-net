using System.Text.Json;
using System.Text.RegularExpressions;
using Lorrgs.WarcraftLogs.Models;
using Lorrgs.WarcraftLogs.ReferenceData;
using Microsoft.Extensions.Logging;

namespace Lorrgs.WarcraftLogs.Services;

public sealed class CompRankingService(WarcraftLogsClient client, IReportTimelineService timelines,
    ILogger<CompRankingService> logger, IWarcraftReferenceData referenceData) : ICompRankingService
{
    private static readonly string RankingsQuery = GraphQlResourceLoader.Load("Rankings.CompRankings.graphql");
    private static readonly string RosterQuery = GraphQlResourceLoader.Load("Rankings.CompRoster.graphql");
    private readonly IWarcraftReferenceData World = referenceData;

    public async Task<CompRankingData?> LoadAsync(CompRankingSearch search, CancellationToken cancellationToken)
    {
        if (search.EncounterId <= 0)
        {
            var encounterId = await EncounterResolver.ResolveAsync(client, search.ZoneId,
                search.BossSlug, search.Edition, search.Refresh, cancellationToken);
            if (encounterId is null)
            {
                logger.LogWarning("Encounter {BossSlug} was not returned for zone {ZoneId}",
                    search.BossSlug, search.ZoneId);
                return null;
            }
            search = search with { EncounterId = encounterId.Value };
        }
        var data = await client.QueryGraphQLAsync(RankingsQuery,
            new { encounterId = search.EncounterId, difficulty = search.Difficulty, page = search.Page },
            search.Edition, search.Refresh, cancellationToken);
        var encounter = data.GetProperty("worldData").GetProperty("encounter");
        if (encounter.ValueKind == JsonValueKind.Null) return null;
        var rankingData = encounter.GetProperty("fightRankings");
        var entries = rankingData.GetProperty("rankings");
        var result = new CompRankingData
        {
            BossSlug = search.BossSlug, Difficulty = search.Difficulty.ToString(),
            Updated = DateTime.UtcNow, Page = search.Page,
            HasMorePages = rankingData.TryGetProperty("hasMorePages", out var more) && more.GetBoolean(),
            TotalCount = entries.GetArrayLength()
        };
        var seen = new HashSet<(string, int)>();
        foreach (var entry in entries.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var duration = (int)Number(entry, "duration");
            if (duration <= 0 || Number(entry, "startTime") <= 0)
                throw new InvalidDataException("Warcraft Logs returned invalid fight timing.");
            var reportRef = entry.GetProperty("report");
            var code = reportRef.GetProperty("code").GetString()!;
            var fightId = (int)Number(reportRef, "fightID");
            if (string.IsNullOrWhiteSpace(code) || fightId <= 0)
                throw new InvalidDataException("Warcraft Logs returned an invalid report reference.");
            if (!seen.Add((code, fightId))) continue;
            var fight = new RankingFight
            {
                FightId = fightId, EncounterId = search.EncounterId,
                Name = EncounterName(search.EncounterId, encounter.GetProperty("name").GetString() ?? search.BossSlug),
                Duration = duration, IsKill = true,
                StartTime = DateTimeOffset.FromUnixTimeMilliseconds((long)Number(entry, "startTime")).UtcDateTime,
                Deaths = (int)Number(entry, "deaths"),
                DamageTaken = Number(entry, "damageTaken")
            };
            var report = new RankingReport
            {
                ReportId = code, Title = Text(entry, "guildName") is { Length: > 0 } guild ? guild : code,
                Duration = duration, StartTime = fight.StartTime, Fights = [fight]
            };
            ApplyRaidIdentity(report, entry);
            result.Reports.Add(report);
        }
        return result;
    }

    public async Task<RankingReport> LoadDetailsAsync(string code, int fightId, string edition,
        bool refresh, CancellationToken cancellationToken)
    {
        JsonElement data;
        try
        {
            data = await client.QueryGraphQLAsync(RosterQuery, new { code, fightIds = new[] { fightId } },
                edition, refresh, cancellationToken);
        }
        catch (WarcraftLogsGraphQlException ex) when (ex.Errors.Count > 0 && ex.Errors.All(error =>
            error.Message?.Contains("report has been archived", StringComparison.OrdinalIgnoreCase) == true))
        {
            logger.LogWarning(ex, "Composition report archived report={ReportCode}", code);
            throw new WarcraftLogsReportPrivateException("This Warcraft Logs report is archived.");
        }
        var metadata = data.GetProperty("reportData").GetProperty("report");
        if (metadata.ValueKind == JsonValueKind.Null)
            throw new WarcraftLogsReportNotFoundException("Warcraft Logs report not found.");
        var fightData = metadata.GetProperty("fights").EnumerateArray()
            .FirstOrDefault(fight => Number(fight, "id") == fightId);
        if (fightData.ValueKind != JsonValueKind.Object)
            throw new WarcraftLogsReportNotFoundException("Warcraft Logs fight not found.");
        var players = ParseRoster(metadata.GetProperty("table"));
        var characterIds = ApplyParsePercentiles(players, metadata, fightId);
        if (players.Count == 0)
            throw new InvalidDataException("Warcraft Logs returned a fight without a player roster.");
        var start = Number(fightData, "startTime");
        var duration = (int)(Number(fightData, "endTime") - start);
        if (duration <= 0) throw new InvalidDataException("Warcraft Logs returned invalid fight timing.");
        var fight = new RankingFight
        {
            FightId = fightId, Name = EncounterName((int)Number(fightData, "encounterID"), Text(fightData, "name")), Duration = duration,
            EncounterId = (int)Number(fightData, "encounterID"), IsKill = true,
            StartTime = DateTimeOffset.FromUnixTimeMilliseconds((long)(Number(metadata, "startTime") + start)).UtcDateTime,
            Composition = BuildComposition(players)
        };
        var report = new RankingReport
        {
            ReportId = code, Title = Text(metadata, "title"),
            Duration = duration, StartTime = fight.StartTime, Fights = [fight], Players = players
        };
        ApplyRaidRankings(fight, metadata);
        foreach (var field in new[] { "damageRankings", "healingRankings" })
        {
            if (!metadata.TryGetProperty(field, out var rankings) || rankings.ValueKind != JsonValueKind.Object ||
                !rankings.TryGetProperty("data", out var rankedFights) || rankedFights.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var entry in rankedFights.EnumerateArray().Where(item => Number(item, "fightID") == fightId))
                ApplyRaidIdentity(report, entry);
        }
        ReportTimelineService.ApplyFightMetadata(report, metadata.GetProperty("fights"));
        await Task.WhenAll(
            PopulateGuildsAsync(players, characterIds, edition, refresh, cancellationToken),
            timelines.PopulateRosterTimelinesAsync(report, edition, refresh, cancellationToken));
        return report;
    }

    private static string EncounterName(int encounterId, string upstreamName) =>
        // Warcraft Logs labels Anniversary Zul'jin (NPC 23863) as Daakara.
        encounterId == 101194 ? "Zul'jin" : upstreamName;

    private static void ApplyRaidIdentity(RankingReport report, JsonElement entry)
    {
        if (entry.TryGetProperty("guild", out var guild) && guild.ValueKind == JsonValueKind.Object)
        {
            report.GuildName ??= Text(guild, "name");
            if (guild.TryGetProperty("server", out var server) && server.ValueKind == JsonValueKind.Object)
            {
                report.ServerName ??= Text(server, "name");
                report.ServerRegion ??= Text(server, "region");
            }
        }
        if (entry.TryGetProperty("server", out var realm) && realm.ValueKind == JsonValueKind.Object)
        {
            report.ServerName ??= Text(realm, "name");
            report.ServerRegion ??= Text(realm, "region");
        }
    }

    private static void ApplyRaidRankings(RankingFight fight, JsonElement metadata)
    {
        foreach (var field in new[] { "damageRankings", "healingRankings" })
        {
            if (!metadata.TryGetProperty(field, out var rankings) || rankings.ValueKind != JsonValueKind.Object ||
                !rankings.TryGetProperty("data", out var fights) || fights.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var entry in fights.EnumerateArray().Where(item => Number(item, "fightID") == fight.FightId))
            {
                if (entry.TryGetProperty("speed", out var speed) && speed.ValueKind == JsonValueKind.Object)
                    fight.Speed ??= speed.Deserialize<RaidFightRanking>(JsonSerializerOptions.Web);
                if (entry.TryGetProperty("execution", out var execution) && execution.ValueKind == JsonValueKind.Object)
                    fight.Execution ??= execution.Deserialize<RaidFightRanking>(JsonSerializerOptions.Web);
            }
        }
    }

    internal List<RankingPlayer> ParseRoster(JsonElement table)
    {
        var details = table.GetProperty("data").GetProperty("playerDetails");
        var players = new List<RankingPlayer>();
        foreach (var group in details.EnumerateObject())
        {
            if (group.Value.ValueKind != JsonValueKind.Array) continue;
            foreach (var item in group.Value.EnumerateArray())
            {
                var id = (int)Number(item, "id");
                if (players.Any(player => player.PlayerId == id)) continue;
                var className = Text(item, "type");
                var wowClass = World.Classes.FirstOrDefault(cls => Normalize(cls.Name) == Normalize(className));
                var specs = item.GetProperty("specs");
                var firstSpec = specs.EnumerateArray().FirstOrDefault();
                var specName = firstSpec.ValueKind == JsonValueKind.String ? firstSpec.GetString()! : Text(firstSpec, "spec");
                var spec = World.Specs.FirstOrDefault(spec => spec.ClassId == wowClass?.Id &&
                    Normalize(spec.Name) == Normalize(specName));
                var slug = spec?.FullNameSlug ?? $"{Slug(specName)}-{wowClass?.NameSlug ?? Slug(className)}";
                var roleCode = group.Name.ToLowerInvariant() switch
                {
                    "tanks" => "tank",
                    "healers" => "heal",
                    _ => World.Roles.FirstOrDefault(role => role.Id == spec?.RoleId)?.Code ??
                        (wowClass?.NameSlug is "warrior" or "rogue" or "death-knight" or "demon-hunter" or "monk" ? "mdps" : "rdps")
                };
                players.Add(new RankingPlayer
                {
                    PlayerId = id, Name = Text(item, "name"), ClassId = wowClass?.Id ?? 0,
                    SpecId = spec?.Id ?? 0, SpecSlug = slug,
                    ServerName = Text(item, "server"), ServerRegion = Text(item, "region"),
                    RoleCode = roleCode
                });
            }
        }
        return players;
    }

    internal Dictionary<int, int> ApplyParsePercentiles(List<RankingPlayer> players, JsonElement metadata, int fightId)
    {
        var characterIds = new Dictionary<int, int>();
        foreach (var player in players)
        {
            var field = player.RoleCode == "heal" ? "healingRankings" : "damageRankings";
            if (!metadata.TryGetProperty(field, out var rankings) || rankings.ValueKind != JsonValueKind.Object ||
                !rankings.TryGetProperty("data", out var fights) || fights.ValueKind != JsonValueKind.Array)
                continue;
            var role = player.RoleCode switch { "tank" => "tanks", "heal" => "healers", _ => "dps" };
            foreach (var fight in fights.EnumerateArray().Where(item => Number(item, "fightID") == fightId))
            {
                if (!fight.TryGetProperty("roles", out var roles) ||
                    !roles.TryGetProperty(role, out var group) ||
                    !group.TryGetProperty("characters", out var characters)) continue;
                var className = World.Classes.FirstOrDefault(item => item.Id == player.ClassId)?.Name;
                var matches = characters.EnumerateArray().Where(character =>
                    string.Equals(Text(character, "name"), player.Name, StringComparison.OrdinalIgnoreCase) &&
                    Normalize(Text(character, "class")) == Normalize(className ?? "") &&
                    (string.IsNullOrEmpty(player.ServerName) ||
                     (character.TryGetProperty("server", out var server) &&
                      string.Equals(Text(server, "name"), player.ServerName, StringComparison.OrdinalIgnoreCase)))).ToArray();
                if (matches.Length != 1) continue;
                player.Performance = Number(matches[0], "amount");
                var characterId = checked((int)Number(matches[0], "id"));
                if (characterId > 0) characterIds[player.PlayerId] = characterId;
                if (matches[0].TryGetProperty("server", out var characterServer))
                {
                    if (string.IsNullOrEmpty(player.ServerRegion))
                        player.ServerRegion = Text(characterServer, "region");
                }
                if (matches[0].TryGetProperty("rankPercent", out var percent) &&
                    percent.ValueKind == JsonValueKind.Number &&
                    percent.TryGetDouble(out var value) && double.IsFinite(value) && value is >= 0 and <= 100)
                    player.ParsePercentile = value;
            }
        }
        return characterIds;
    }

    private async Task PopulateGuildsAsync(List<RankingPlayer> players, Dictionary<int, int> characterIds,
        string edition, bool refresh, CancellationToken cancellationToken)
    {
        await Task.WhenAll(characterIds.Values.Distinct().Order().Chunk(20).Select(async chunk =>
        {
            var fields = chunk.Select(id => $"c{id}: character(id: {id}) {{ guilds {{ name }} }}");
            var query = $"query CompPlayerGuilds {{ characterData {{ {string.Join(" ", fields)} }} }}";
            var data = await client.QueryGraphQLAsync(query, edition: edition,
                bypassCache: refresh, cancellationToken: cancellationToken);
            if (!data.TryGetProperty("characterData", out var characters) ||
                characters.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Warcraft Logs returned no character metadata.");
            foreach (var player in players)
            {
                if (!characterIds.TryGetValue(player.PlayerId, out var id) ||
                    !characters.TryGetProperty($"c{id}", out var character) ||
                    character.ValueKind != JsonValueKind.Object ||
                    !character.TryGetProperty("guilds", out var guilds) ||
                    guilds.ValueKind != JsonValueKind.Array || guilds.GetArrayLength() != 1)
                    continue;
                player.GuildName = Text(guilds[0], "name");
            }
        }));
    }

    private CompRankingComposition BuildComposition(IEnumerable<RankingPlayer> players)
    {
        var composition = new CompRankingComposition();
        foreach (var player in players)
        {
            var spec = World.GetSpec(player.SpecSlug);
            var role = player.RoleCode ?? World.Roles.FirstOrDefault(role => role.Id == spec?.RoleId)?.Code ?? "unknown";
            var classSlug = World.Classes.FirstOrDefault(cls => cls.Id == player.ClassId)?.NameSlug ?? "unknown";
            Increment(composition.Specs, player.SpecSlug);
            Increment(composition.Roles, role);
            Increment(composition.Classes, classSlug);
        }
        return composition;
    }

    private static void Increment(Dictionary<string, int> counts, string key) => counts[key] = counts.GetValueOrDefault(key) + 1;
    private static string Normalize(string value) => string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();
    private static string Slug(string value) => Regex.Replace(value.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-");
    private static string Text(JsonElement element, string key) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()! : "";
    private static double Number(JsonElement element, string key) =>
        element.TryGetProperty(key, out var value) && value.TryGetDouble(out var number) ? number : 0;
}
