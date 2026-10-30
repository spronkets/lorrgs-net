using System.Text.Json;
using Lorrgs.WarcraftLogs.Models;
using Microsoft.Extensions.Logging;

namespace Lorrgs.WarcraftLogs.Services;

public sealed class ReportTimelineService(
    WarcraftLogsClient client, ILogger<ReportTimelineService> logger) : IReportTimelineService
{
    private static readonly string MetadataQuery = GraphQlResourceLoader.Load("Timeline.Metadata.graphql");
    private static readonly string ReportMetadataQuery = GraphQlResourceLoader.Load("Timeline.ReportMetadata.graphql");
    private static readonly string EventsQuery = GraphQlResourceLoader.Load("Timeline.Events.graphql");
    private static readonly string BossMetadataQuery = GraphQlResourceLoader.Load("Timeline.BossMetadata.graphql");
    private static readonly string BossEventsQuery = GraphQlResourceLoader.Load("Timeline.BossEvents.graphql");
    private static readonly string PhasesQuery = GraphQlResourceLoader.Load("Timeline.Phases.graphql");
    private static readonly string MasterDataQuery = GraphQlResourceLoader.Load("Timeline.MasterData.graphql");

    public async Task PopulateFightPhasesAsync(IReadOnlyList<RankingReport> reports, string edition,
        CancellationToken cancellationToken)
    {
        foreach (var report in reports.Where(report => report.Fights.Any(fight => fight.Phases is null)).ToArray())
        {
            var shared = await client.TryQueryGraphQLFromCacheAsync(ReportMetadataQuery,
                new { code = report.ReportId }, edition, cancellationToken);
            if (shared is not null)
                ApplyFightMetadata(report, GetReport(shared.Value).GetProperty("fights"));
            foreach (var fight in report.Fights.Where(fight => fight.Phases is null))
            {
                var cached = await client.TryQueryGraphQLFromCacheAsync(BossMetadataQuery,
                    new { code = report.ReportId, fightIds = new[] { fight.FightId } }, edition, cancellationToken);
                if (cached is not null)
                {
                    try
                    {
                        ApplyFightMetadata(report, GetReport(cached.Value).GetProperty("fights"));
                    }
                    catch (WarcraftLogsReportNotFoundException)
                    {
                        logger.LogWarning("Cached phase metadata report unavailable");
                        RemoveReport(reports, report);
                    }
                }
            }
        }
        var groups = reports.Where(report => report.Fights.Any(fight => fight.Phases is null))
            .GroupBy(report => report.ReportId).OrderBy(group => group.Key, StringComparer.Ordinal).ToArray();
        await PopulatePhaseChunksAsync(reports, groups.Chunk(20), edition, cancellationToken);
    }

    private async Task PopulatePhaseChunksAsync(IReadOnlyList<RankingReport> reports,
        IEnumerable<IGrouping<string, RankingReport>[]> chunks, string edition, CancellationToken cancellationToken)
    {
        var results = await Task.WhenAll(chunks.Select(async chunk =>
        {
            var localReports = chunk.SelectMany(group => group).ToList();
            await PopulatePhaseChunkAsync(localReports, chunk, edition, cancellationToken);
            return (Original: chunk.SelectMany(group => group).ToArray(), Retained: localReports);
        }));
        foreach (var result in results)
            foreach (var report in result.Original.Except(result.Retained))
                RemoveReport(reports, report);
    }

    private async Task PopulatePhaseChunkAsync(IReadOnlyList<RankingReport> reports,
        IGrouping<string, RankingReport>[] chunk, string edition, CancellationToken cancellationToken)
    {
        if (chunk.All(group => group.All(report => report.Fights.All(fight => fight.Phases is not null))))
            return;
        var variables = new Dictionary<string, object>();
        var definitions = new List<string>();
        var selections = new List<string>();
        for (var index = 0; index < chunk.Length; index++)
        {
            variables[$"code{index}"] = chunk[index].Key;
            variables[$"fights{index}"] = chunk[index].SelectMany(report => report.Fights)
                .Where(fight => fight.Phases is null)
                .Select(fight => fight.FightId).Distinct().Order().ToArray();
            definitions.Add($"$code{index}: String!, $fights{index}: [Int!]");
            selections.Add($"r{index}: report(code: $code{index}) {{ fights(fightIDs: $fights{index}) " +
                "{ id encounterID startTime endTime phaseTransitions { id startTime } } }");
        }
        var query = PhasesQuery.Replace("__VARIABLES__", string.Join(", ", definitions), StringComparison.Ordinal)
            .Replace("__REPORTS__", string.Join("\n", selections), StringComparison.Ordinal);
        JsonElement data;
        try
        {
            data = await client.QueryGraphQLAsync(query, variables, edition, cancellationToken: cancellationToken);
        }
        catch (WarcraftLogsGraphQlException ex) when (ex.IsReportAccessFailure &&
            HasBatchReportAliases(ex, chunk.Length))
        {
            RemoveInaccessibleReports(reports, chunk, ex.InaccessibleReportAliases);
            if (ex.PartialData is not { ValueKind: JsonValueKind.Object } partialData ||
                !partialData.TryGetProperty("reportData", out var partialReports) ||
                partialReports.ValueKind != JsonValueKind.Object)
            {
                await PopulatePhaseChunksAsync(reports,
                    chunk.Where(group => group.Any(reports.Contains)).Select(group => new[] { group }),
                    edition, cancellationToken);
                return;
            }
            data = partialData;
        }
        catch (WarcraftLogsGraphQlException ex) when (ex.IsReportAccessFailure)
        {
            if (chunk.Length == 1)
            {
                logger.LogWarning("Phase metadata report unavailable");
                RemoveInaccessibleReports(reports, chunk, new HashSet<string> { "r0" });
            }
            else
            {
                logger.LogWarning("Retrying failed phase batch individually to identify unavailable reports");
                await PopulatePhaseChunksAsync(reports, chunk.Select(group => new[] { group }),
                    edition, cancellationToken);
            }
            return;
        }
        var reportData = data.GetProperty("reportData");
        for (var index = 0; index < chunk.Length; index++)
        {
            var alias = $"r{index}";
            if (!reportData.TryGetProperty(alias, out var report) || report.ValueKind != JsonValueKind.Object)
            {
                logger.LogWarning("Phase metadata report unavailable");
                RemoveInaccessibleReports(reports, chunk, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { alias });
                continue;
            }
            foreach (var rankingReport in chunk[index])
                ApplyFightMetadata(rankingReport, report.GetProperty("fights"));
        }
    }

    internal static void ApplyFightMetadata(RankingReport report, JsonElement fights)
    {
        foreach (var fightData in fights.EnumerateArray())
            foreach (var fight in report.Fights.Where(fight => fight.Phases is null && fight.FightId == Number(fightData, "id")))
            {
                if (fightData.TryGetProperty("encounterID", out var encounter) && encounter.ValueKind == JsonValueKind.Number)
                    fight.EncounterId = encounter.GetInt32();
                fight.Phases = ObservedPhases(fightData, Number(fightData, "startTime"), Number(fightData, "endTime"));
            }
    }

    private void RemoveInaccessibleReports(IReadOnlyList<RankingReport> reports,
        IReadOnlyList<IGrouping<string, RankingReport>> chunk, IReadOnlySet<string> aliases)
    {
        foreach (var alias in aliases)
        {
            if (!alias.StartsWith('r') || !int.TryParse(alias.AsSpan(1), out var index) ||
                index < 0 || index >= chunk.Count) continue;
            foreach (var report in chunk[index].ToArray())
                RemoveReport(reports, report);
        }
    }

    private void RemoveReport(IReadOnlyList<RankingReport> reports, RankingReport report)
    {
        if (reports is not IList<RankingReport> { IsReadOnly: false } mutableReports)
            throw new InvalidOperationException("Ranking report collection must support removal of inaccessible reports.");
        logger.LogWarning("Removing inaccessible ranking report");
        mutableReports.Remove(report);
    }

    private static bool HasBatchReportAliases(WarcraftLogsGraphQlException exception, int count) =>
        exception.Errors.Count > 0 && exception.Errors.All(error =>
            error.ReportAlias is { } alias && int.TryParse(alias.AsSpan(1), out var index) &&
            index >= 0 && index < count);

    private static IReadOnlyList<TimelinePhase> ObservedPhases(JsonElement fight, double start, double end) =>
        fight.TryGetProperty("phaseTransitions", out var transitions) && transitions.ValueKind == JsonValueKind.Array
            ? transitions.EnumerateArray().Where(item => Number(item, "startTime") >= start && Number(item, "startTime") <= end)
                .Select(item => new TimelinePhase(checked((int)Number(item, "id")), Number(item, "startTime") - start))
                .OrderBy(item => item.Timestamp).ToArray()
            : [];

    public async Task<BossTimeline?> LoadBossAsync(string reportCode, int fightId, string edition,
        bool cacheOnly, CancellationToken cancellationToken)
    {
        var sharedMetadata = await client.TryQueryGraphQLFromCacheAsync(ReportMetadataQuery,
            new { code = reportCode }, edition, cancellationToken);
        if (sharedMetadata is not null)
        {
            var sharedReport = GetReport(sharedMetadata.Value);
            var sharedFight = sharedReport.GetProperty("fights").EnumerateArray()
                .FirstOrDefault(item => Number(item, "id") == fightId);
            if (sharedFight.ValueKind == JsonValueKind.Object)
            {
                var sharedEvents = await ReadReportEventsAsync(reportCode, sharedReport, sharedFight,
                    edition, true, false, cancellationToken);
                if (sharedEvents is { Complete: true })
                    return BuildBossTimeline(reportCode, sharedReport, sharedFight, sharedEvents.Value.Events, edition);
            }
        }
        var metadataData = await QueryAsync(BossMetadataQuery,
            new { code = reportCode, fightIds = new[] { fightId } }, edition, cacheOnly, cancellationToken);
        if (metadataData is null) return null;
        var metadata = GetReport(metadataData.Value);
        var fights = metadata.GetProperty("fights").EnumerateArray().Where(f => Number(f, "id") == fightId).ToArray();
        if (fights.Length != 1) throw new KeyNotFoundException("The requested fight was not found.");
        var fight = fights[0];
        using var verifiedScope = BeginVerifiedReportScope(metadata, fight);
        var start = Number(fight, "startTime");
        var end = Number(fight, "endTime");
        if (end <= start) throw new InvalidOperationException("Warcraft Logs returned an invalid fight duration.");
        var pages = await ReadEventsAsync(BossEventsQuery, cursor => new
        {
            code = reportCode,
            fightIds = new[] { fightId },
            startTime = cursor,
            endTime = end
        }, start, end, edition, cacheOnly, cancellationToken);
        if (pages is null || (cacheOnly && !pages.Value.Complete)) return null;
        var events = pages.Value.Events.Where(item => Text(item, "type") == "cast" &&
            Number(item, "abilityGameID") > 0 && Number(item, "timestamp") >= start && Number(item, "timestamp") <= end)
            .Select(item => new TimelineEvent(checked((int)Number(item, "abilityGameID")),
                Number(item, "timestamp") - start, null, "cast")).OrderBy(item => item.Timestamp).ToArray();
        var phases = ObservedPhases(fight, start, end);
        var encounterId = checked((int)Number(fight, "encounterID"));
        // Older unexpired metadata cache entries contain the name but not encounterID.
        if (encounterId == 0 && string.Equals(Text(fight, "name"), "Nek'zali the Soulcoiler",
                StringComparison.OrdinalIgnoreCase))
            encounterId = 3470;
        if (string.Equals(edition, "retail", StringComparison.OrdinalIgnoreCase))
            phases = TimelineCorrections.CorrectPhases(encounterId, phases, events, end - start, pages.Value.Complete);
        if (!pages.Value.Complete)
            logger.LogWarning("Boss timeline page limit reached");
        return new BossTimeline(reportCode, fightId, Text(fight, "name") ?? "Boss",
            end - start, pages.Value.Complete, ObservedSpells(metadata.GetProperty("masterData"), events), events, phases);
    }

    public async Task<ReportTimeline> LoadAsync(string reportCode, int fightId, int? sourceId,
        string? playerName, string edition, CancellationToken cancellationToken, int? specId = null)
    {
        return await LoadCoreAsync(reportCode, fightId, sourceId, playerName, edition, false, cancellationToken, specId)
            ?? throw new InvalidOperationException("The requested timeline could not be loaded.");
    }

    public Task<ReportTimeline?> TryLoadCachedAsync(string reportCode, int fightId, int? sourceId,
        string? playerName, string edition, CancellationToken cancellationToken, int? specId = null) =>
        LoadCoreAsync(reportCode, fightId, sourceId, playerName, edition, true, cancellationToken, specId);

    public async Task PopulateRosterTimelinesAsync(RankingReport report, string edition, bool refresh,
        CancellationToken cancellationToken)
    {
        var fight = report.Fights.Single();
        var data = await client.QueryGraphQLAsync(ReportMetadataQuery,
            new { code = report.ReportId },
            edition, refresh, cancellationToken);
        var metadata = GetReport(data);
        var fights = metadata.GetProperty("fights").EnumerateArray().ToArray();
        var timing = fights.Single(item => Number(item, "id") == fight.FightId);
        using var verifiedScope = BeginVerifiedReportScope(metadata, timing);
        var start = Number(timing, "startTime");
        var end = Number(timing, "endTime");
        if (end <= start)
            throw new InvalidDataException("Warcraft Logs returned invalid fight timing.");
        var master = metadata.GetProperty("masterData");
        var actors = master.GetProperty("actors").EnumerateArray().ToArray();
        foreach (var player in report.Players)
        {
            if (!actors.Any(actor => Number(actor, "id") == player.PlayerId &&
                string.Equals(Text(actor, "name"), player.Name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("A roster player could not be identified in the report.");
        }
        var pages = await ReadReportEventsAsync(report.ReportId, metadata, timing,
            edition, false, refresh, cancellationToken);
        var (rawEvents, complete) = pages
            ?? throw new InvalidDataException("Warcraft Logs returned no roster timeline data.");
        foreach (var player in report.Players)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var events = TimelineCorrections.CorrectEvents(
                NormalizeEvents(rawEvents, player.PlayerId, start, end, complete, ActorNames(master, edition)), player.SpecId, edition, complete);
            var maxHealth = ResourceMaximum(rawEvents, player.PlayerId, false, start, end);
            player.Timeline = new ReportTimeline(report.ReportId, fight.FightId, player.PlayerId, player.Name,
                end - start, complete, TimelineCorrections.CorrectSpells(ObservedSpells(master, events), events), events)
            {
                MaxHealth = maxHealth,
                EstimatedMaxHealth = maxHealth is null ? EstimateMaxHealth(events) : null,
                HealthCapacities = HealthCapacities(rawEvents, events, player.PlayerId, start, end),
                MaxMana = ResourceMaximum(rawEvents, player.PlayerId, true, start, end)
            };
        }
        report.BossTimeline = BuildBossTimeline(report.ReportId, metadata, timing, rawEvents, edition);
        var persistedMetadata = await client.TryQueryGraphQLFromCacheAsync(ReportMetadataQuery,
            new { code = report.ReportId }, edition, cancellationToken);
        if (persistedMetadata is not null &&
            await ReadReportEventsAsync(report.ReportId, GetReport(persistedMetadata.Value), timing,
                edition, true, false, cancellationToken) is { Complete: true })
            await RemoveCoveredPlayerEventsAsync(report.ReportId, metadata, edition, cancellationToken);
    }

    private async Task RemoveCoveredPlayerEventsAsync(string reportCode, JsonElement metadata,
        string edition, CancellationToken cancellationToken)
    {
        var actors = metadata.GetProperty("masterData").GetProperty("actors").EnumerateArray().ToArray();
        foreach (var fight in metadata.GetProperty("fights").EnumerateArray())
        {
            var fightId = checked((int)Number(fight, "id"));
            var start = Number(fight, "startTime");
            var end = Number(fight, "endTime");
            foreach (var actor in actors)
            {
                if (Text(actor, "type") is { } actorType && actorType != "Player") continue;
                var name = Text(actor, "name");
                if (string.IsNullOrEmpty(name)) continue;
                var escaped = name.Replace("\\", "\\\\", StringComparison.Ordinal)
                    .Replace("'", "\\'", StringComparison.Ordinal);
                var filter = $"(source.name='{escaped}' AND type IN ('begincast','cast','heal','damage')) OR " +
                    $"(target.name='{escaped}' AND type IN ('applybuff','refreshbuff','removebuff','applybuffstack','removebuffstack','applydebuff','refreshdebuff','removedebuff','applydebuffstack','removedebuffstack','death','heal','damage','resourcechange','drain','interrupt'))";
                var cursor = start;
                for (var page = 0; page < 3; page++)
                {
                    var variables = new
                    {
                        code = reportCode, fightIds = new[] { fightId },
                        startTime = cursor, endTime = end, filter
                    };
                    var cached = await client.TryQueryGraphQLFromCacheAsync(EventsQuery, variables, edition, cancellationToken);
                    if (cached is null) break;
                    var events = GetReport(cached.Value).GetProperty("events");
                    await client.RemoveGraphQlCacheEntryAsync(EventsQuery, variables, edition, cancellationToken);
                    if (!events.TryGetProperty("nextPageTimestamp", out var next) ||
                        next.ValueKind == JsonValueKind.Null) break;
                    var nextCursor = next.GetDouble();
                    if (nextCursor >= end || nextCursor <= cursor) break;
                    cursor = nextCursor;
                }
            }
        }
    }

    private Task<(List<JsonElement> Events, bool Complete)?> ReadReportEventsAsync(
        string reportCode, JsonElement metadata, JsonElement fight, string edition,
        bool cacheOnly, bool refresh, CancellationToken cancellationToken)
    {
        var fights = metadata.GetProperty("fights").EnumerateArray().ToArray();
        var reportStart = fights.Min(item => Number(item, "startTime"));
        var reportEnd = fights.Max(item => Number(item, "endTime"));
        var start = Number(fight, "startTime");
        var end = Number(fight, "endTime");
        const string filter = "type IN ('begincast','cast','applybuff','refreshbuff','removebuff','applybuffstack','removebuffstack','applydebuff','refreshdebuff','removedebuff','applydebuffstack','removedebuffstack','death','heal','damage','resourcechange','drain','interrupt')";
        return ReadEventsAsync(EventsQuery, cursor => new
        {
            code = reportCode,
            fightIds = fights.Select(item => checked((int)Number(item, "id"))).Order().ToArray(),
            startTime = cursor, endTime = reportEnd, filter
        }, reportStart, reportEnd, edition, cacheOnly, cancellationToken, refresh, int.MaxValue,
            item => (Number(item, "timestamp") >= start && Number(item, "timestamp") <= end) ||
                (Text(item, "type") is "applybuff" or "removebuff" && Number(item, "timestamp") > end));
    }

    private static BossTimeline BuildBossTimeline(string reportCode, JsonElement metadata,
        JsonElement timing, List<JsonElement> rawEvents, string edition)
    {
        var start = Number(timing, "startTime");
        var end = Number(timing, "endTime");
        var encounterId = checked((int)Number(timing, "encounterID"));
        var enemies = timing.GetProperty("enemyNPCs").EnumerateArray()
            .Select(enemy => checked((int)Number(enemy, "id"))).ToHashSet();
        var bossEvents = rawEvents.Where(item => Text(item, "type") == "cast" &&
            enemies.Contains(checked((int)Number(item, "sourceID"))) &&
            Number(item, "abilityGameID") > 0 &&
            Number(item, "timestamp") >= start && Number(item, "timestamp") <= end)
            .Select(item => new TimelineEvent(checked((int)Number(item, "abilityGameID")),
                Number(item, "timestamp") - start, null, "cast")).OrderBy(item => item.Timestamp).ToArray();
        var phases = ObservedPhases(timing, start, end);
        if (string.Equals(edition, "retail", StringComparison.OrdinalIgnoreCase))
            phases = TimelineCorrections.CorrectPhases(encounterId, phases, bossEvents, end - start, true);
        return new BossTimeline(reportCode, checked((int)Number(timing, "id")),
            encounterId == 101194 ? "Zul'jin" : Text(timing, "name") ?? "Boss",
            end - start, true, ObservedSpells(metadata.GetProperty("masterData"), bossEvents), bossEvents, phases);
    }

    private async Task<JsonElement?> QueryAsync(string query, object variables, string edition,
        bool cacheOnly, CancellationToken cancellationToken) =>
        cacheOnly
            ? await client.TryQueryGraphQLFromCacheAsync(query, variables, edition, cancellationToken)
            : await client.QueryGraphQLAsync(query, variables, edition, cancellationToken: cancellationToken);

    private async Task<ReportTimeline?> LoadCoreAsync(string reportCode, int fightId, int? sourceId,
        string? playerName, string edition, bool cacheOnly, CancellationToken cancellationToken, int? specId)
    {
        var sharedMetadata = await client.TryQueryGraphQLFromCacheAsync(ReportMetadataQuery,
            new { code = reportCode }, edition, cancellationToken);
        var sharedReport = sharedMetadata is null ? (JsonElement?)null : GetReport(sharedMetadata.Value);
        var sharedFight = sharedReport?.GetProperty("fights").EnumerateArray()
            .FirstOrDefault(item => Number(item, "id") == fightId);
        var hasSharedFight = sharedFight is { ValueKind: JsonValueKind.Object };
        var metadataData = hasSharedFight ? sharedMetadata : await QueryAsync(MetadataQuery,
            new { code = reportCode, fightIds = new[] { fightId } }, edition, cacheOnly, cancellationToken);
        if (metadataData is null) return null;
        var metadata = GetReport(metadataData.Value);
        var fights = metadata.GetProperty("fights").EnumerateArray()
            .Where(f => Number(f, "id") == fightId).ToArray();
        if (fights.Length == 0 && !cacheOnly)
        {
            logger.LogWarning("Timeline fight missing from cached metadata; refreshing report metadata");
            metadata = GetReport(await client.QueryGraphQLAsync(MetadataQuery,
                new { code = reportCode, fightIds = new[] { fightId } }, edition,
                bypassCache: true, cancellationToken: cancellationToken));
            fights = metadata.GetProperty("fights").EnumerateArray()
                .Where(f => Number(f, "id") == fightId).ToArray();
        }
        if (fights.Length != 1)
        {
            logger.LogWarning("Timeline fight metadata unavailable cacheOnly={CacheOnly}", cacheOnly);
            if (cacheOnly) return null;
            throw new KeyNotFoundException("The requested fight was not found.");
        }
        using var verifiedScope = BeginVerifiedReportScope(metadata, fights[0]);
        var start = Number(fights[0], "startTime");
        var end = Number(fights[0], "endTime");
        if (end <= start)
            throw new InvalidOperationException("Warcraft Logs returned an invalid fight duration.");

        metadata.TryGetProperty("masterData", out var master);
        if (master.ValueKind != JsonValueKind.Object ||
            !master.TryGetProperty("actors", out var availableActors) || availableActors.ValueKind != JsonValueKind.Array ||
            !master.TryGetProperty("abilities", out var availableAbilities) || availableAbilities.ValueKind != JsonValueKind.Array)
        {
            var lookup = await QueryAsync(MasterDataQuery, new { code = reportCode },
                edition, cacheOnly, cancellationToken);
            if (lookup is not null)
            {
                var lookupReport = GetReport(lookup.Value);
                if (lookupReport.TryGetProperty("masterData", out var lookupMaster) &&
                    lookupMaster.ValueKind == JsonValueKind.Object &&
                    lookupMaster.TryGetProperty("actors", out var lookupActors) &&
                    lookupActors.ValueKind == JsonValueKind.Array &&
                    lookupMaster.TryGetProperty("abilities", out var lookupAbilities) &&
                    lookupAbilities.ValueKind == JsonValueKind.Array)
                {
                    logger.LogInformation("Reusing complete report master data for timeline cacheOnly={CacheOnly}", cacheOnly);
                    master = lookupMaster;
                }
                if (master.ValueKind != JsonValueKind.Object ||
                    !master.TryGetProperty("abilities", out var spellData) || spellData.ValueKind != JsonValueKind.Array)
                {
                    logger.LogWarning("Timeline spell metadata unavailable cacheOnly={CacheOnly}", cacheOnly);
                    if (cacheOnly) return null;
                    throw new InvalidOperationException("Spell metadata is unavailable for this report.");
                }
            }
        }
        if (master.ValueKind != JsonValueKind.Object ||
            !master.TryGetProperty("actors", out var actorData) || actorData.ValueKind != JsonValueKind.Array)
        {
            logger.LogWarning("Timeline actor metadata unavailable cacheOnly={CacheOnly}", cacheOnly);
            if (cacheOnly) return null;
            throw new KeyNotFoundException("Player metadata is unavailable for this report.");
        }
        var actors = actorData.EnumerateArray().Where(actor => actor.ValueKind == JsonValueKind.Object).ToArray();
        var matches = sourceId is > 0
            ? actors.Where(a => Number(a, "id") == sourceId).ToArray()
            : [];
        if (matches.Length == 0 && !string.IsNullOrWhiteSpace(playerName))
            matches = actors.Where(a => string.Equals(Text(a, "name"), playerName.Trim(),
                StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length != 1 || (!string.IsNullOrWhiteSpace(playerName) &&
            !string.Equals(Text(matches[0], "name"), playerName.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            logger.LogWarning("Timeline player could not be resolved cacheOnly={CacheOnly}", cacheOnly);
            if (cacheOnly) return null;
            throw new KeyNotFoundException("The player could not be uniquely identified in this report.");
        }
        var actorId = checked((int)Number(matches[0], "id"));
        var actorName = Text(matches[0], "name")!;
        logger.LogInformation("Resolved timeline player source={SourceId} player={PlayerName}", actorId, actorName);
        var filterName = actorName.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal);
        var filter = $"(source.name='{filterName}' AND type IN ('begincast','cast','heal','damage')) OR " +
            $"(target.name='{filterName}' AND type IN ('applybuff','refreshbuff','removebuff','applybuffstack','removebuffstack','applydebuff','refreshdebuff','removedebuff','applydebuffstack','removedebuffstack','death','heal','damage','resourcechange','drain','interrupt'))";
        var sharedPages = hasSharedFight
            ? await ReadReportEventsAsync(reportCode, metadata, fights[0], edition, true, false, cancellationToken)
            : null;
        var pages = sharedPages is { Complete: true } ? sharedPages : await ReadEventsAsync(EventsQuery, cursor => new
        {
            code = reportCode,
            fightIds = new[] { fightId },
            startTime = cursor,
            endTime = end,
            filter
        }, start, end, edition, cacheOnly, cancellationToken);
        if (pages is null) return null;
        var (rawEvents, complete) = pages.Value;
        if (cacheOnly && !complete) return null;
        if (!complete)
            logger.LogWarning("Timeline page limit reached source={SourceId}; returning explicitly incomplete events", actorId);

        var events = TimelineCorrections.CorrectEvents(
            NormalizeEvents(rawEvents, actorId, start, end, complete, ActorNames(master, edition)), specId, edition, complete);
        if (events.Count == 0)
            logger.LogWarning("WarcraftLogs returned no matching timeline events source={SourceId} rawEventCount={RawEventCount}",
                actorId, rawEvents.Count);
        var maxHealth = ResourceMaximum(rawEvents, actorId, false, start, end);
        return new ReportTimeline(reportCode, fightId, actorId, Text(matches[0], "name")!,
            end - start, complete, TimelineCorrections.CorrectSpells(ObservedSpells(master, events), events), events)
        {
            MaxHealth = maxHealth,
            EstimatedMaxHealth = maxHealth is null ? EstimateMaxHealth(events) : null,
            HealthCapacities = HealthCapacities(rawEvents, events, actorId, start, end),
            MaxMana = ResourceMaximum(rawEvents, actorId, true, start, end)
        };
    }

    private IDisposable? BeginVerifiedReportScope(JsonElement metadata, JsonElement fight)
    {
        var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ReportCode"] = Text(metadata, "code"),
            ["ReportTitle"] = Text(metadata, "title"),
            ["FightId"] = Number(fight, "id"),
            ["EncounterId"] = Number(fight, "encounterID"),
            ["EncounterName"] = Text(fight, "name")
        });
        logger.LogInformation("Retrieved timeline report and fight");
        return scope;
    }

    private async Task<(List<JsonElement> Events, bool Complete)?> ReadEventsAsync(string query,
        Func<double, object> variables, double start, double end, string edition, bool cacheOnly,
        CancellationToken cancellationToken, bool refresh = false, int maxPages = 3,
        Func<JsonElement, bool>? retainEvent = null)
    {
        var rawEvents = new List<JsonElement>();
        var cursor = start;
        var complete = false;
        for (var page = 0; page < maxPages; page++)
        {
            var eventData = refresh
                ? await client.QueryGraphQLAsync(query, variables(cursor), edition, true, cancellationToken)
                : await QueryAsync(query, variables(cursor), edition, cacheOnly, cancellationToken);
            if (eventData is null) return null;
            var report = GetReport(eventData.Value);
            var paginator = report.GetProperty("events");
            foreach (var item in paginator.GetProperty("data").EnumerateArray())
                if (retainEvent is null || retainEvent(item))
                    rawEvents.Add(item.Clone());
            if (!paginator.TryGetProperty("nextPageTimestamp", out var next) ||
                next.ValueKind == JsonValueKind.Null)
            {
                complete = true;
                break;
            }
            var nextCursor = next.GetDouble();
            if (nextCursor >= end)
            {
                complete = true;
                break;
            }
            if (nextCursor <= cursor)
            {
                if (cacheOnly)
                {
                    logger.LogWarning("Cached Warcraft Logs timeline pagination did not advance; ignoring incomplete cache coverage");
                    return null;
                }
                throw new InvalidOperationException("Warcraft Logs timeline pagination did not advance.");
            }
            cursor = nextCursor;
        }
        return (rawEvents, complete);
    }

    private static IReadOnlyList<TimelineSpell> ObservedSpells(JsonElement master, IReadOnlyList<TimelineEvent> events)
    {
        var usedIds = events.Select(e => e.SpellId).Where(id => id > 0).ToHashSet();
        var spells = new Dictionary<int, TimelineSpell>();
        var abilities = master.ValueKind == JsonValueKind.Object &&
            master.TryGetProperty("abilities", out var abilityData) && abilityData.ValueKind == JsonValueKind.Array
                ? abilityData.EnumerateArray().Where(ability => ability.ValueKind == JsonValueKind.Object)
                : Enumerable.Empty<JsonElement>();
        foreach (var ability in abilities)
        {
            var id = checked((int)Number(ability, "gameID"));
            if (usedIds.Contains(id))
                spells[id] = new TimelineSpell(id, Text(ability, "name") ?? $"Spell {id}", Text(ability, "icon"));
        }
        foreach (var id in usedIds)
            spells.TryAdd(id, new TimelineSpell(id, $"Spell {id}", null));
        return spells.Values.OrderBy(s => s.Name).ToArray();
    }

    internal static double? ResourceMaximum(IEnumerable<JsonElement> events, int actorId, bool mana,
        double start = double.MinValue, double end = double.MaxValue)
    {
        double? maximum = null;
        foreach (var item in events)
        {
            if (Number(item, "timestamp") < start || Number(item, "timestamp") > end) continue;
            if (!HasActorResources(item, actorId)) continue;
            if (!mana)
            {
                var health = Number(item, "maxHitPoints");
                // Some reports expose percentage-only health snapshots with a maximum of 100.
                if (health > 0 && health != 100) maximum = Math.Max(maximum ?? 0, health);
            }
            else if (item.TryGetProperty("classResources", out var resources) && resources.ValueKind == JsonValueKind.Array)
                foreach (var resource in resources.EnumerateArray())
                    if (resource.TryGetProperty("type", out var type) && type.TryGetInt32(out var id) && id == 0)
                    {
                        var capacity = Number(resource, "max");
                        if (capacity > 0) maximum = Math.Max(maximum ?? 0, capacity);
                    }
        }
        return maximum;
    }

    private static bool HasActorResources(JsonElement item, int actorId)
    {
        // Explicit snapshot ownership takes precedence over event-type conventions.
        var actorField = item.TryGetProperty("resourceActor", out _)
            ? Number(item, "resourceActor") switch { 1 => "sourceID", 2 => "targetID", _ => null }
            : Text(item, "type") == "cast" ? "sourceID" :
                Text(item, "type") is "heal" or "damage" ? "targetID" : null;
        return actorField is not null && Number(item, actorField) == actorId;
    }

    private static double? HealthPercent(JsonElement item, int actorId)
    {
        if (!HasActorResources(item, actorId) ||
            !item.TryGetProperty("hitPoints", out var health) || !health.TryGetDouble(out var current) ||
            !item.TryGetProperty("maxHitPoints", out var maximum) || !maximum.TryGetDouble(out var max) ||
            max <= 0 || current < 0 || current > max)
            return null;
        return current / max * 100;
    }

    internal static double? EstimateMaxHealth(IEnumerable<TimelineEvent> events)
    {
        // Large percentage changes limit rounding error; the median resists individual noisy samples.
        var estimates = events
            .Where(item => item.Kind is "heal" or "hot" &&
                item.HealthBeforeIsPreviousSnapshot && item.Amount is > 0 &&
                item.HealthBeforePercent.HasValue && item.HealthAfterPercent.HasValue &&
                item.HealthAfterPercent.Value - item.HealthBeforePercent.Value >= 10)
            .Select(item => item.Amount!.Value * 100 /
                (item.HealthAfterPercent!.Value - item.HealthBeforePercent!.Value))
            .Order()
            .ToArray();
        if (estimates.Length == 0) return null;
        var middle = estimates.Length / 2;
        return Math.Round(estimates.Length % 2 == 0
            ? (estimates[middle - 1] + estimates[middle]) / 2 : estimates[middle]);
    }

    // These auras change haste, mana recovery, critical chance, or armor, not health capacity.
    private static readonly HashSet<int> NonHealthCapacityAuras =
        [2825, 32182, 24907, 33736, 16188, 16166, 16237];

    private static bool IsHealthCapacityBoundary(JsonElement item, int actorId) =>
        Number(item, "targetID") == actorId && (Text(item, "type") == "death" ||
        !NonHealthCapacityAuras.Contains(checked((int)Number(item, "abilityGameID"))) && Text(item, "type") is
            "applybuff" or "refreshbuff" or "removebuff" or "applybuffstack" or "removebuffstack" or
            "applydebuff" or "refreshdebuff" or "removedebuff" or "applydebuffstack" or "removedebuffstack");

    internal static IReadOnlyList<TimelineHealthCapacity> HealthCapacities(
        IEnumerable<JsonElement> rawEvents, IReadOnlyList<TimelineEvent> events, int actorId, double start, double end)
    {
        var estimates = events.Where(item => item.Kind is "heal" or "hot")
            .GroupBy(item => item.Timestamp).ToDictionary(group => group.Key, group => EstimateMaxHealth(group));
        var result = new List<TimelineHealthCapacity>();
        double? recorded = null;
        double? estimated = null;
        foreach (var group in rawEvents.Where(item => Number(item, "timestamp") >= start &&
                     Number(item, "timestamp") <= end).GroupBy(item => Number(item, "timestamp")).OrderBy(group => group.Key))
        {
            var boundary = group.Any(item => IsHealthCapacityBoundary(item, actorId));
            if (boundary)
            {
                recorded = null;
                estimated = null;
            }
            var snapshot = group.LastOrDefault(item => HasActorResources(item, actorId) &&
                Number(item, "maxHitPoints") > 0);
            if (snapshot.ValueKind != JsonValueKind.Undefined)
            {
                var maximum = Number(snapshot, "maxHitPoints");
                recorded = maximum != 100 ? maximum : null;
                if (recorded.HasValue) estimated = null;
            }
            // Same-timestamp aura changes make a heal-based estimate ambiguous.
            if (!boundary && recorded is null && estimates.GetValueOrDefault(group.Key - start) is { } estimate)
                estimated = estimate;
            if (result.Count == 0 || result[^1].MaxHealth != recorded || result[^1].EstimatedMaxHealth != estimated)
                result.Add(new TimelineHealthCapacity(group.Key - start, recorded, estimated));
        }
        return result;
    }

    private static IReadOnlyDictionary<int, string> ActorNames(JsonElement master, string edition) =>
        master.GetProperty("actors").EnumerateArray()
            .Where(actor => Text(actor, "name") is not null)
            .GroupBy(actor => checked((int)Number(actor, "id")))
            .ToDictionary(group => group.Key, group =>
                edition == "anniversary" && Text(group.First(), "type") == "NPC" &&
                Number(group.First(), "gameID") == 23863
                    ? "Zul'jin" : Text(group.First(), "name")!);

    internal static IReadOnlyList<TimelineEvent> NormalizeEvents(
        IEnumerable<JsonElement> rawEvents, int actorId, double start, double end, bool complete,
        IReadOnlyDictionary<int, string>? actorNames = null)
    {
        var result = new List<TimelineEvent>();
        var orderedEvents = rawEvents.OrderBy(e => Number(e, "timestamp")).ToArray();
        var castResults = MatchedCastResults(orderedEvents, actorId, start, end, actorNames);
        var buffs = new Dictionary<int, double>();
        double? previousHealthPercent = null;
        double previousHealthTimestamp = double.NegativeInfinity;
        (int SpellId, double Timestamp)? pendingCast = null;
        foreach (var item in orderedEvents)
        {
            var timestamp = Number(item, "timestamp");
            if (timestamp < start) continue;
            if (timestamp > end)
            {
                if (Text(item, "type") == "applybuff" && Number(item, "targetID") == actorId)
                    buffs.Remove(checked((int)Number(item, "abilityGameID")));
                if (Text(item, "type") == "removebuff" && Number(item, "targetID") == actorId &&
                    buffs.Remove(checked((int)Number(item, "abilityGameID")), out var appliedBeforeEnd))
                    result.Add(new TimelineEvent(checked((int)Number(item, "abilityGameID")),
                        appliedBeforeEnd, end - start - appliedBeforeEnd, "buff"));
                continue;
            }
            var relative = timestamp - start;
            var type = Text(item, "type");
            var spellId = checked((int)Number(item, "abilityGameID"));
            var sourceName = actorNames?.GetValueOrDefault(checked((int)Number(item, "sourceID")));
            if (IsHealthCapacityBoundary(item, actorId)) previousHealthPercent = null;
            var healthBefore = timestamp - previousHealthTimestamp <= 1000 ? previousHealthPercent : null;
            var healthAfter = HealthPercent(item, actorId);
            if (healthAfter.HasValue)
            {
                previousHealthPercent = healthAfter;
                previousHealthTimestamp = timestamp;
            }
            else if (HasActorResources(item, actorId) ||
                type is "death" or "heal" or "damage" && Number(item, "targetID") == actorId)
                previousHealthPercent = null;
            if (type == "begincast" && Number(item, "sourceID") == actorId && spellId > 0)
                pendingCast = (spellId, relative);
            if (type is "interrupt" or "death" && Number(item, "targetID") == actorId)
                pendingCast = null;
            if (type == "cast" && Number(item, "sourceID") == actorId && spellId > 0)
            {
                var castStart = pendingCast is { } pending && pending.SpellId == spellId
                    ? pending.Timestamp : (double?)null;
                pendingCast = null;
                result.Add(new TimelineEvent(spellId, relative,
                    castStart.HasValue ? relative - castStart.Value : null, "cast", CastStart: castStart,
                    CastResults: castResults.GetValueOrDefault((spellId, timestamp))));
                if (item.TryGetProperty("classResources", out var resources) && resources.ValueKind == JsonValueKind.Array)
                    foreach (var resource in resources.EnumerateArray())
                        if (resource.TryGetProperty("type", out var resourceType) && resourceType.TryGetInt32(out var id) && id == 0 &&
                            resource.TryGetProperty("cost", out var cost) && cost.TryGetDouble(out var amount) && amount > 0)
                            result.Add(new TimelineEvent(spellId, relative, null, "mana-spent", Amount: amount, SourceName: sourceName));
            }
            if (Number(item, "targetID") != actorId) continue;
            if (type == "death")
                result.Add(new TimelineEvent(0, relative, null, "death"));
            if (spellId <= 0) continue;
            if (type is "resourcechange" or "drain" &&
                item.TryGetProperty("resourceChangeType", out var changeType) && changeType.TryGetInt32(out var manaType) && manaType == 0 &&
                item.TryGetProperty("resourceChange", out var change) && change.TryGetDouble(out var mana))
            {
                var amount = type == "drain" ? mana : mana - Number(item, "waste");
                if (amount > 0)
                    result.Add(new TimelineEvent(spellId, relative, null,
                        type == "drain" ? "mana-spent" : "mana-returned", Amount: amount, SourceName: sourceName,
                        IsManaDrain: type == "drain"));
            }
            if (type == "damage")
            {
                var damage = item.TryGetProperty("amount", out var damageAmount) && damageAmount.TryGetDouble(out var amount)
                    ? amount : (double?)null;
                var previousSnapshot = healthBefore.HasValue;
                if (healthAfter.HasValue && Number(item, "maxHitPoints") != 100 && damage is >= 0)
                {
                    var before = Number(item, "hitPoints") + damage.Value - Math.Max(0, Number(item, "overkill"));
                    healthBefore = before >= 0 && before <= Number(item, "maxHitPoints")
                        ? before / Number(item, "maxHitPoints") * 100 : null;
                    previousSnapshot = false;
                }
                result.Add(new TimelineEvent(spellId, relative, null, "damage",
                    Amount: damage, SourceName: sourceName,
                    HealthBeforePercent: healthBefore, HealthAfterPercent: healthAfter,
                    HealthBeforeIsPreviousSnapshot: previousSnapshot));
            }
            if (type == "heal")
            {
                var healing = item.TryGetProperty("amount", out var healAmount) && healAmount.TryGetDouble(out var amount)
                    ? amount : (double?)null;
                var previousSnapshot = healthBefore.HasValue;
                // Absolute HP allows an exact pre-heal value; percentages cannot be reduced by a raw healing amount.
                if (healthAfter.HasValue && Number(item, "maxHitPoints") != 100 && healing is >= 0)
                {
                    var before = Number(item, "hitPoints") - healing.Value;
                    healthBefore = before >= 0 ? before / Number(item, "maxHitPoints") * 100 : null;
                    previousSnapshot = false;
                }
                result.Add(new TimelineEvent(spellId, relative, null,
                    item.TryGetProperty("tick", out var tick) && tick.ValueKind == JsonValueKind.True
                        ? "hot" : "heal",
                    Amount: healing, SourceName: sourceName,
                    HealthBeforePercent: healthBefore, HealthAfterPercent: healthAfter,
                    HealthBeforeIsPreviousSnapshot: previousSnapshot));
            }
            if (type == "applybuff")
                buffs.TryAdd(spellId, relative);
            else if (type == "removebuff" && buffs.Remove(spellId, out var applied))
                result.Add(new TimelineEvent(spellId, applied, relative - applied, "buff"));
            // A refresh extends the same measured interval; no guessed prepull application.
        }
        return result.OrderBy(e => e.Timestamp).ThenBy(e => e.SpellId).ToArray();
    }

    private static Dictionary<(int SpellId, double Timestamp), IReadOnlyList<TimelineCastResult>> MatchedCastResults(
        JsonElement[] events, int actorId, double start, double end, IReadOnlyDictionary<int, string>? actorNames)
    {
        var castEvents = events.Where(item => Text(item, "type") == "cast" && Number(item, "sourceID") == actorId &&
                Number(item, "timestamp") >= start && Number(item, "timestamp") <= end).ToArray();
        var casts = castEvents
            .GroupBy(item => checked((int)Number(item, "abilityGameID")))
            .ToDictionary(group => group.Key, group => group.Select(item => Number(item, "timestamp")).ToArray());
        var castTargets = castEvents
            .GroupBy(item => (checked((int)Number(item, "abilityGameID")), Number(item, "timestamp")))
            .ToDictionary(group => group.Key, group => checked((int)Number(group.First(), "targetID")));
        var results = new Dictionary<(int, double), List<TimelineCastResult>>();
        foreach (var item in events)
        {
            var kind = Text(item, "type");
            var timestamp = Number(item, "timestamp");
            if (kind is not ("heal" or "damage") || Number(item, "sourceID") != actorId ||
                timestamp < start || timestamp > end ||
                item.TryGetProperty("tick", out var tick) && tick.ValueKind == JsonValueKind.True ||
                !item.TryGetProperty("amount", out var amountValue) || !amountValue.TryGetDouble(out var amount) ||
                amount <= 0)
                continue;
            var spellId = checked((int)Number(item, "abilityGameID"));
            if (!casts.TryGetValue(spellId, out var timestamps)) continue;
            var index = Array.BinarySearch(timestamps, timestamp);
            if (index < 0) index = ~index - 1;
            if (index < 0 || timestamp - timestamps[index] > 3000 ||
                index > 0 && timestamps[index - 1] == timestamps[index] ||
                index + 1 < timestamps.Length && timestamps[index + 1] == timestamps[index])
                continue;
            var targetId = checked((int)Number(item, "targetID"));
            if (targetId <= 0) continue;
            var targetName = actorNames?.GetValueOrDefault(targetId) ?? $"Actor {targetId}";
            var key = (spellId, timestamps[index]);
            var delayed = timestamp - timestamps[index] > 250;
            // Delayed direct hits need the cast's recorded target; never infer projectile AoE or repeated ticks.
            if (delayed && (kind != "damage" || castTargets[key] != targetId ||
                item.TryGetProperty("isAoE", out var aoe) && aoe.ValueKind == JsonValueKind.True))
                continue;
            if (!results.TryGetValue(key, out var entries))
                results[key] = entries = [];
            var existing = entries.FindIndex(entry => entry.TargetId == targetId && entry.Kind == kind);
            if (delayed && existing >= 0) continue;
            if (existing < 0) entries.Add(new TimelineCastResult(targetId, targetName, kind, amount));
            else entries[existing] = entries[existing] with { Amount = entries[existing].Amount + amount };
        }
        return results.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<TimelineCastResult>)pair.Value);
    }

    private static JsonElement GetReport(JsonElement data)
    {
        if (!data.TryGetProperty("reportData", out var reports) ||
            !reports.TryGetProperty("report", out var report) || report.ValueKind != JsonValueKind.Object)
            throw new WarcraftLogsReportNotFoundException("The Warcraft Logs report was not found.");
        return report;
    }

    private static double Number(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number
            ? property.GetDouble() : 0;

    private static string? Text(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() : null;
}
