using System.Text.Json;
using Lorrgs.Api.Models;
using Lorrgs.WarcraftLogs;

namespace Lorrgs.Api.Services;

public interface IReportTimelineService
{
    Task<ReportTimeline> LoadAsync(string reportCode, int fightId, int? sourceId,
        string? playerName, string edition, CancellationToken cancellationToken);
    Task<ReportTimeline?> TryLoadCachedAsync(string reportCode, int fightId, int? sourceId,
        string? playerName, string edition, CancellationToken cancellationToken);
}

public sealed class ReportTimelineService(
    WarcraftLogsClient client, ILogger<ReportTimelineService> logger) : IReportTimelineService
{
    private static readonly string MetadataQuery = GraphQlResourceLoader.Load("Timeline.Metadata.graphql");
    private static readonly string EventsQuery = GraphQlResourceLoader.Load("Timeline.Events.graphql");

    public async Task<ReportTimeline> LoadAsync(string reportCode, int fightId, int? sourceId,
        string? playerName, string edition, CancellationToken cancellationToken)
    {
        return await LoadCoreAsync(reportCode, fightId, sourceId, playerName, edition, false, cancellationToken)
            ?? throw new InvalidOperationException("The requested timeline could not be loaded.");
    }

    public Task<ReportTimeline?> TryLoadCachedAsync(string reportCode, int fightId, int? sourceId,
        string? playerName, string edition, CancellationToken cancellationToken) =>
        LoadCoreAsync(reportCode, fightId, sourceId, playerName, edition, true, cancellationToken);

    private async Task<JsonElement?> QueryAsync(string query, object variables, string edition,
        bool cacheOnly, CancellationToken cancellationToken) =>
        cacheOnly
            ? await client.TryQueryGraphQLFromCacheAsync(query, variables, edition, cancellationToken)
            : await client.QueryGraphQLAsync(query, variables, edition, cancellationToken: cancellationToken);

    private async Task<ReportTimeline?> LoadCoreAsync(string reportCode, int fightId, int? sourceId,
        string? playerName, string edition, bool cacheOnly, CancellationToken cancellationToken)
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

        var master = metadata.GetProperty("masterData");
        var actors = master.GetProperty("actors").EnumerateArray().ToArray();
        var matches = sourceId is > 0
            ? actors.Where(a => Number(a, "id") == sourceId).ToArray()
            : [];
        if (matches.Length == 0 && !string.IsNullOrWhiteSpace(playerName))
            matches = actors.Where(a => string.Equals(Text(a, "name"), playerName.Trim(),
                StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length != 1 || (!string.IsNullOrWhiteSpace(playerName) &&
            !string.Equals(Text(matches[0], "name"), playerName.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new KeyNotFoundException("The player could not be uniquely identified in this report.");
        var actorId = checked((int)Number(matches[0], "id"));
        var actorName = Text(matches[0], "name")!;
        var filterName = actorName.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal);
        var filter = $"(source.name='{filterName}' AND type='cast') OR " +
            $"(target.name='{filterName}' AND type IN ('applybuff','refreshbuff','removebuff','death'))";
        var rawEvents = new List<JsonElement>();
        var cursor = start;
        var complete = false;
        for (var page = 0; page < 3; page++)
        {
            var eventData = await QueryAsync(EventsQuery, new
            {
                code = reportCode, fightIds = new[] { fightId }, startTime = cursor,
                endTime = end, filter
            }, edition, cacheOnly, cancellationToken);
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
        if (cacheOnly && !complete) return null;
        if (!complete)
            logger.LogWarning("Timeline page limit reached report={ReportCode} fight={FightId} source={SourceId}; returning explicitly incomplete events",
                reportCode, fightId, actorId);

        var events = NormalizeEvents(rawEvents, actorId, start, end, complete);
        if (events.Count == 0)
            logger.LogWarning("WarcraftLogs returned no matching timeline events report={ReportCode} fight={FightId} source={SourceId} rawEventCount={RawEventCount}",
                reportCode, fightId, actorId, rawEvents.Count);
        var usedIds = events.Select(e => e.SpellId).Where(id => id > 0).ToHashSet();
        var spells = new Dictionary<int, TimelineSpell>();
        foreach (var ability in master.GetProperty("abilities").EnumerateArray())
        {
            var id = checked((int)Number(ability, "gameID"));
            if (usedIds.Contains(id))
                spells[id] = new TimelineSpell(id, Text(ability, "name") ?? $"Spell {id}", Text(ability, "icon"));
        }
        foreach (var id in usedIds)
            spells.TryAdd(id, new TimelineSpell(id, $"Spell {id}", null));
        return new ReportTimeline(reportCode, fightId, actorId, Text(matches[0], "name")!,
            end - start, complete, spells.Values.OrderBy(s => s.Name).ToArray(), events);
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
                result.Add(new TimelineEvent(spellId, applied, end - start - applied, "buff"));
        return result.OrderBy(e => e.Timestamp).ThenBy(e => e.SpellId).ToArray();
    }

    private static JsonElement GetReport(JsonElement data)
    {
        if (!data.TryGetProperty("reportData", out var reports) ||
            !reports.TryGetProperty("report", out var report) || report.ValueKind != JsonValueKind.Object)
            throw new KeyNotFoundException("The report was not found or is inaccessible.");
        return report;
    }

    private static double Number(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number
            ? property.GetDouble() : 0;

    private static string? Text(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() : null;
}
