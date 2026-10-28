using System.Text.Json;
using Lorrgs.Api.Models;
using Lorrgs.WarcraftLogs;

namespace Lorrgs.Api.Services;

public interface IReportTimelineService
{
    Task<ReportTimeline> LoadAsync(string reportCode, int fightId, int? sourceId,
        string? playerName, string edition, CancellationToken cancellationToken, int? specId = null);
    Task<ReportTimeline?> TryLoadCachedAsync(string reportCode, int fightId, int? sourceId,
        string? playerName, string edition, CancellationToken cancellationToken, int? specId = null);
    Task<BossTimeline?> LoadBossAsync(string reportCode, int fightId, string edition,
        bool cacheOnly, CancellationToken cancellationToken);
    Task PopulateFightPhasesAsync(IReadOnlyList<RankingReport> reports, string edition,
        CancellationToken cancellationToken);
}

public sealed class ReportTimelineService(
    WarcraftLogsClient client, ILogger<ReportTimelineService> logger) : IReportTimelineService
{
    private static readonly string MetadataQuery = GraphQlResourceLoader.Load("Timeline.Metadata.graphql");
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
                    catch (WarcraftLogsReportNotFoundException ex)
                    {
                        logger.LogWarning(ex, "Cached phase metadata report unavailable report={ReportCode}", report.ReportId);
                        RemoveReport(reports, report);
                    }
                }
            }
        }
        var groups = reports.Where(report => report.Fights.Count > 0)
            .GroupBy(report => report.ReportId).OrderBy(group => group.Key, StringComparer.Ordinal).ToArray();
        foreach (var chunk in groups.Chunk(20))
            await PopulatePhaseChunkAsync(reports, chunk, edition, cancellationToken);
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
                foreach (var group in chunk.Where(group => group.Any(reports.Contains)))
                    await PopulatePhaseChunkAsync(reports, [group], edition, cancellationToken);
                return;
            }
            data = partialData;
        }
        catch (WarcraftLogsGraphQlException ex) when (ex.IsReportAccessFailure)
        {
            if (chunk.Length == 1)
            {
                logger.LogWarning(ex, "Phase metadata report unavailable report={ReportCode}", chunk[0].Key);
                RemoveInaccessibleReports(reports, chunk, new HashSet<string> { "r0" });
            }
            else
            {
                logger.LogWarning(ex, "Retrying failed phase batch individually to identify unavailable reports");
                foreach (var group in chunk)
                    await PopulatePhaseChunkAsync(reports, [group], edition, cancellationToken);
            }
            return;
        }
        var reportData = data.GetProperty("reportData");
        for (var index = 0; index < chunk.Length; index++)
        {
            var alias = $"r{index}";
            if (!reportData.TryGetProperty(alias, out var report) || report.ValueKind != JsonValueKind.Object)
            {
                logger.LogWarning("Phase metadata report unavailable report={ReportCode}", chunk[index].Key);
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
        logger.LogWarning("Removing inaccessible ranking report={ReportCode}", report.ReportId);
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
        var metadataData = await QueryAsync(BossMetadataQuery,
            new { code = reportCode, fightIds = new[] { fightId } }, edition, cacheOnly, cancellationToken);
        if (metadataData is null) return null;
        var metadata = GetReport(metadataData.Value);
        var fights = metadata.GetProperty("fights").EnumerateArray().Where(f => Number(f, "id") == fightId).ToArray();
        if (fights.Length != 1) throw new KeyNotFoundException("The requested fight was not found.");
        var fight = fights[0];
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
            logger.LogWarning("Boss timeline page limit reached report={ReportCode} fight={FightId}", reportCode, fightId);
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

    private async Task<JsonElement?> QueryAsync(string query, object variables, string edition,
        bool cacheOnly, CancellationToken cancellationToken) =>
        cacheOnly
            ? await client.TryQueryGraphQLFromCacheAsync(query, variables, edition, cancellationToken)
            : await client.QueryGraphQLAsync(query, variables, edition, cancellationToken: cancellationToken);

    private async Task<ReportTimeline?> LoadCoreAsync(string reportCode, int fightId, int? sourceId,
        string? playerName, string edition, bool cacheOnly, CancellationToken cancellationToken, int? specId)
    {
        var metadataData = await QueryAsync(MetadataQuery,
            new { code = reportCode, fightIds = new[] { fightId } }, edition, cacheOnly, cancellationToken);
        if (metadataData is null) return null;
        var metadata = GetReport(metadataData.Value);
        var fights = metadata.GetProperty("fights").EnumerateArray()
            .Where(f => Number(f, "id") == fightId).ToArray();
        if (fights.Length != 1)
            throw new KeyNotFoundException("The requested fight was not found.");
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
                    logger.LogInformation("Reusing complete report master data for timeline report={ReportCode} fight={FightId} cacheOnly={CacheOnly}",
                        reportCode, fightId, cacheOnly);
                    master = lookupMaster;
                }
                if (master.ValueKind != JsonValueKind.Object ||
                    !master.TryGetProperty("abilities", out var spellData) || spellData.ValueKind != JsonValueKind.Array)
                {
                    logger.LogWarning("Timeline spell metadata unavailable report={ReportCode} fight={FightId} cacheOnly={CacheOnly}",
                        reportCode, fightId, cacheOnly);
                    if (cacheOnly) return null;
                    throw new InvalidOperationException("Spell metadata is unavailable for this report.");
                }
            }
        }
        if (master.ValueKind != JsonValueKind.Object ||
            !master.TryGetProperty("actors", out var actorData) || actorData.ValueKind != JsonValueKind.Array)
        {
            logger.LogWarning("Timeline actor metadata unavailable report={ReportCode} fight={FightId} cacheOnly={CacheOnly}",
                reportCode, fightId, cacheOnly);
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
            logger.LogWarning("Timeline player could not be resolved report={ReportCode} fight={FightId} source={SourceId} player={PlayerName} cacheOnly={CacheOnly}",
                reportCode, fightId, sourceId, playerName, cacheOnly);
            if (cacheOnly) return null;
            throw new KeyNotFoundException("The player could not be uniquely identified in this report.");
        }
        var actorId = checked((int)Number(matches[0], "id"));
        var actorName = Text(matches[0], "name")!;
        var filterName = actorName.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal);
        var filter = $"(source.name='{filterName}' AND type='cast') OR " +
            $"(target.name='{filterName}' AND type IN ('applybuff','refreshbuff','removebuff','death'))";
        var pages = await ReadEventsAsync(EventsQuery, cursor => new
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
            logger.LogWarning("Timeline page limit reached report={ReportCode} fight={FightId} source={SourceId}; returning explicitly incomplete events",
                reportCode, fightId, actorId);

        var events = TimelineCorrections.CorrectEvents(
            NormalizeEvents(rawEvents, actorId, start, end, complete), specId, edition, complete);
        if (events.Count == 0)
            logger.LogWarning("WarcraftLogs returned no matching timeline events report={ReportCode} fight={FightId} source={SourceId} rawEventCount={RawEventCount}",
                reportCode, fightId, actorId, rawEvents.Count);
        return new ReportTimeline(reportCode, fightId, actorId, Text(matches[0], "name")!,
            end - start, complete, TimelineCorrections.CorrectSpells(ObservedSpells(master, events), events), events);
    }

    private async Task<(List<JsonElement> Events, bool Complete)?> ReadEventsAsync(string query,
        Func<double, object> variables, double start, double end, string edition, bool cacheOnly,
        CancellationToken cancellationToken)
    {
        var rawEvents = new List<JsonElement>();
        var cursor = start;
        var complete = false;
        for (var page = 0; page < 3; page++)
        {
            var eventData = await QueryAsync(query, variables(cursor), edition, cacheOnly, cancellationToken);
            if (eventData is null) return null;
            var report = GetReport(eventData.Value);
            var paginator = report.GetProperty("events");
            foreach (var item in paginator.GetProperty("data").EnumerateArray())
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
                throw new InvalidOperationException("Warcraft Logs timeline pagination did not advance.");
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

    internal static IReadOnlyList<TimelineEvent> NormalizeEvents(
        IEnumerable<JsonElement> rawEvents, int actorId, double start, double end, bool complete)
    {
        var result = new List<TimelineEvent>();
        var buffs = new Dictionary<int, double>();
        foreach (var item in rawEvents.OrderBy(e => Number(e, "timestamp")))
        {
            var timestamp = Number(item, "timestamp");
            if (timestamp < start || timestamp > end) continue;
            var relative = timestamp - start;
            var type = Text(item, "type");
            var spellId = checked((int)Number(item, "abilityGameID"));
            if (type == "cast" && Number(item, "sourceID") == actorId && spellId > 0)
                result.Add(new TimelineEvent(spellId, relative, null, "cast"));
            if (Number(item, "targetID") != actorId) continue;
            if (type == "death")
                result.Add(new TimelineEvent(0, relative, null, "death"));
            if (spellId <= 0) continue;
            if (type == "applybuff")
                buffs.TryAdd(spellId, relative);
            else if (type == "removebuff" && buffs.Remove(spellId, out var applied))
                result.Add(new TimelineEvent(spellId, applied, relative - applied, "buff"));
            // A refresh extends the same measured interval; no guessed prepull application.
        }
        if (complete)
            foreach (var (spellId, applied) in buffs)
                result.Add(new TimelineEvent(spellId, applied, end - start - applied, "buff", true));
        return result.OrderBy(e => e.Timestamp).ThenBy(e => e.SpellId).ToArray();
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
