using System.Text.Json;
using Lorrgs.Api.Models;
using WclClient = Lorrgs.WarcraftLogs.WarcraftLogsClient;

namespace Lorrgs.Api.Services;

public class RotationAnalysisService(
    ILogger<RotationAnalysisService> logger,
    WclClient warcraftLogsClient)
{
    private readonly ILogger<RotationAnalysisService> _logger = logger;
    private readonly WclClient _warcraftLogsClient = warcraftLogsClient;

    private static readonly string ReportMetadataQuery =
            GraphQlResourceLoader.Load("Rotation.ReportMetadata.graphql");

    private static readonly string RotationEventsQuery =
            GraphQlResourceLoader.Load("Rotation.RotationEvents.graphql");

    public async Task<RotationAnalysisResponse> AnalyzeAsync(RotationAnalysisRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ReportCode))
        {
            throw new ArgumentException("ReportCode is required.");
        }

        var metadata = await _warcraftLogsClient.QueryGraphQLAsync(
            ReportMetadataQuery,
            new { code = request.ReportCode.Trim() }
        );

        if (!TryGetReport(metadata, out var report))
        {
            throw new InvalidOperationException("Report not found or inaccessible.");
        }

        var selectedFight = ResolveFight(report, request.FightId);
        var selectedActor = ResolveActor(report, request.SourceId, request.PlayerName);

        var fightStart = selectedFight.startTime;
        var fightEnd = selectedFight.endTime;
        var eventLimit = Math.Clamp(request.EventLimit, 100, 10000);
        var timeline = new List<RotationEventPoint>();
        var pageStart = fightStart;

        while (pageStart < fightEnd && timeline.Count < eventLimit)
        {
            var pagePayload = await _warcraftLogsClient.QueryGraphQLAsync(
                RotationEventsQuery,
                new
                {
                    code = request.ReportCode.Trim(),
                    startTime = pageStart,
                    endTime = fightEnd,
                    sourceId = selectedActor.id,
                    fightIds = new[] { selectedFight.id },
                    limit = eventLimit
                }
            );

            var page = ParseRotationEvents(pagePayload);
            if (page.Events.Count == 0)
            {
                break;
            }

            timeline.AddRange(page.Events);

            if (!page.NextPageTimestamp.HasValue || page.NextPageTimestamp.Value <= pageStart)
            {
                break;
            }

            pageStart = (long)Math.Ceiling(page.NextPageTimestamp.Value);
        }

        timeline = timeline.OrderBy(point => point.Timestamp).ToList();
        var abilitySummary = BuildAbilitySummary(timeline);
        var durationSeconds = Math.Max((fightEnd - fightStart) / 1000.0, 1);
        var castsPerMinute = timeline.Count / (durationSeconds / 60.0);

        var (score, notes, verdict) = ScoreRotation(
            castsPerMinute,
            abilitySummary,
            request.ParsePercentile,
            request.ItemLevel
        );

        return new RotationAnalysisResponse
        {
            ReportCode = request.ReportCode.Trim(),
            FightId = selectedFight.id,
            FightName = selectedFight.name,
            PlayerName = selectedActor.name,
            SourceId = selectedActor.id,
            SpecHint = selectedActor.subType,
            RoleHint = InferRole(selectedActor.subType),
            GameExpansionHint = TryGetNestedString(report.element, "zone", "name"),
            ParsePercentile = request.ParsePercentile,
            ItemLevel = request.ItemLevel,
            FightDurationSeconds = durationSeconds,
            CastsPerMinute = Math.Round(castsPerMinute, 2),
            Verdict = verdict,
            Score = score,
            Notes = notes,
            AbilitySummary = abilitySummary,
            RotationTimeline = timeline.Take(200).ToList()
        };
    }

    private static (int score, List<string> notes, string verdict) ScoreRotation(
        double castsPerMinute,
        List<RotationAbilitySummary> abilitySummary,
        int? parsePercentile,
        int? itemLevel)
    {
        var score = 40;
        var notes = new List<string>();

        if (castsPerMinute >= 25)
        {
            score += 20;
            notes.Add("Strong cast activity for fight duration.");
        }
        else if (castsPerMinute >= 18)
        {
            score += 10;
            notes.Add("Reasonable cast activity with room to tighten globals.");
        }
        else
        {
            notes.Add("Low cast activity; likely downtime or missed globals.");
        }

        var distinctAbilities = abilitySummary.Count;
        if (distinctAbilities >= 8)
        {
            score += 10;
            notes.Add("Good ability coverage instead of repetitive single-button usage.");
        }
        else if (distinctAbilities <= 3)
        {
            notes.Add("Very narrow ability usage pattern; may indicate suboptimal rotation.");
        }

        if (parsePercentile.HasValue)
        {
            if (parsePercentile.Value >= 85)
            {
                score += 20;
                notes.Add("High parse supports an optimal or near-optimal rotation.");
            }
            else if (parsePercentile.Value >= 60)
            {
                score += 10;
                notes.Add("Mid parse suggests mostly solid execution.");
            }
            else
            {
                notes.Add("Lower parse suggests rotational or uptime issues.");
            }
        }
        else
        {
            notes.Add("No parse percentile supplied, using rotation-only heuristic.");
        }

        if (itemLevel.HasValue)
        {
            if (itemLevel.Value < 500 && (parsePercentile ?? 0) >= 70)
            {
                score += 8;
                notes.Add("Good output relative to lower gear indicates efficient rotation.");
            }
            else if (itemLevel.Value >= 520 && (parsePercentile ?? 0) < 50)
            {
                notes.Add("Gear level appears high relative to parse; rotation may be underperforming.");
            }
        }

        score = Math.Clamp(score, 0, 100);
        var verdict = score >= 70 ? "optimal" : "not_optimal";
        return (score, notes, verdict);
    }

    private static string InferRole(string specHint)
    {
        var spec = specHint?.ToLowerInvariant() ?? string.Empty;
        if (spec.Contains("holy") || spec.Contains("restoration") || spec.Contains("discipline") || spec.Contains("mistweaver") || spec.Contains("preservation"))
        {
            return "healer";
        }

        if (spec.Contains("protection") || spec.Contains("blood") || spec.Contains("guardian") || spec.Contains("vengeance") || spec.Contains("brewmaster"))
        {
            return "tank";
        }

        return "dps";
    }

    private static string? TryGetNestedString(JsonElement element, string parent, string child)
    {
        if (!element.TryGetProperty(parent, out var parentElement))
        {
            return null;
        }

        if (!parentElement.TryGetProperty(child, out var childElement))
        {
            return null;
        }

        return childElement.GetString();
    }

    private static List<RotationAbilitySummary> BuildAbilitySummary(List<RotationEventPoint> timeline)
    {
        var total = Math.Max(timeline.Count, 1);
        var grouped = timeline
            .GroupBy(point => point.Ability)
            .Select(group =>
            {
                var ordered = group.OrderBy(point => point.Timestamp).ToList();
                var intervals = new List<double>();

                for (var i = 1; i < ordered.Count; i++)
                {
                    intervals.Add((ordered[i].Timestamp - ordered[i - 1].Timestamp) / 1000.0);
                }

                intervals.Sort();
                double? median = null;

                if (intervals.Count > 0)
                {
                    var mid = intervals.Count / 2;
                    median = intervals.Count % 2 == 0
                        ? (intervals[mid - 1] + intervals[mid]) / 2.0
                        : intervals[mid];
                }

                return new RotationAbilitySummary
                {
                    Ability = group.Key,
                    CastCount = group.Count(),
                    SharePercent = Math.Round((group.Count() * 100.0) / total, 2),
                    MedianIntervalSeconds = median
                };
            })
            .OrderByDescending(summary => summary.CastCount)
            .Take(20)
            .ToList();

        return grouped;
    }

    private static RotationEventPage ParseRotationEvents(JsonElement eventsPayload)
    {
        var timeline = new List<RotationEventPoint>();
        double? nextPageTimestamp = null;

        if (!eventsPayload.TryGetProperty("reportData", out var reportData) ||
            !reportData.TryGetProperty("report", out var report) ||
            !report.TryGetProperty("events", out var eventsNode) ||
            !eventsNode.TryGetProperty("data", out var eventsData) ||
            eventsData.ValueKind != JsonValueKind.Array)
        {
            return new RotationEventPage(timeline, nextPageTimestamp);
        }

        if (eventsNode.TryGetProperty("nextPageTimestamp", out var nextPageNode) &&
            nextPageNode.ValueKind == JsonValueKind.Number &&
            nextPageNode.TryGetDouble(out var nextPageValue))
        {
            nextPageTimestamp = nextPageValue;
        }

        foreach (var item in eventsData.EnumerateArray())
        {
            if (!item.TryGetProperty("abilityGameID", out _) || !item.TryGetProperty("timestamp", out var timestampElement))
            {
                continue;
            }

            string ability;
            if (item.TryGetProperty("ability", out var abilityNode) && abilityNode.TryGetProperty("name", out var abilityName))
            {
                ability = abilityName.GetString() ?? "Unknown";
            }
            else
            {
                ability = $"Spell:{item.GetProperty("abilityGameID").GetInt32()}";
            }

            timeline.Add(new RotationEventPoint
            {
                Timestamp = timestampElement.GetInt64(),
                Ability = ability,
                EventType = item.TryGetProperty("type", out var typeNode) ? typeNode.GetString() : "cast"
            });
        }

        return new RotationEventPage(timeline, nextPageTimestamp);
    }

    private static (int id, string name, string subType) ResolveActor(JsonReport report, int? sourceId, string? playerName)
    {
        if (!report.element.TryGetProperty("masterData", out var masterData) ||
            !masterData.TryGetProperty("actors", out var actors) ||
            actors.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("No actors found in report metadata.");
        }

        var allActors = actors.EnumerateArray()
            .Where(actor => actor.TryGetProperty("id", out _) && actor.TryGetProperty("name", out _))
            .Select(actor => new
            {
                Id = actor.GetProperty("id").GetInt32(),
                Name = actor.GetProperty("name").GetString() ?? string.Empty,
                SubType = actor.TryGetProperty("subType", out var subTypeNode) ? subTypeNode.GetString() ?? "Unknown" : "Unknown"
            })
            .ToList();

        if (sourceId.HasValue)
        {
            var byId = allActors.FirstOrDefault(actor => actor.Id == sourceId.Value);
            if (byId is null)
            {
                throw new InvalidOperationException($"Player sourceId {sourceId.Value} not found in this report.");
            }

            return (byId.Id, byId.Name, byId.SubType);
        }

        if (!string.IsNullOrWhiteSpace(playerName))
        {
            var byName = allActors.FirstOrDefault(actor => string.Equals(actor.Name, playerName, StringComparison.OrdinalIgnoreCase));
            if (byName is null)
            {
                throw new InvalidOperationException($"Player '{playerName}' not found in this report.");
            }

            return (byName.Id, byName.Name, byName.SubType);
        }

        var fallback = allActors.FirstOrDefault();
        if (fallback is null)
        {
            throw new InvalidOperationException("No players found in this report.");
        }

        return (fallback.Id, fallback.Name, fallback.SubType);
    }

    private static (int id, string name, long startTime, long endTime) ResolveFight(JsonReport report, int? fightId)
    {
        if (!report.element.TryGetProperty("fights", out var fights) || fights.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("No fights found in report metadata.");
        }

        var list = fights.EnumerateArray()
            .Where(fight => fight.TryGetProperty("id", out _) && fight.TryGetProperty("name", out _))
            .Select(fight => new
            {
                Id = fight.GetProperty("id").GetInt32(),
                Name = fight.GetProperty("name").GetString() ?? "Unknown",
                Start = fight.GetProperty("startTime").GetInt64(),
                End = fight.GetProperty("endTime").GetInt64(),
                Kill = fight.TryGetProperty("kill", out var killNode) && killNode.GetBoolean()
            })
            .Where(fight => fight.End > fight.Start)
            .ToList();

        if (list.Count == 0)
        {
            throw new InvalidOperationException("No valid fights found in report.");
        }

        if (fightId.HasValue)
        {
            var explicitFight = list.FirstOrDefault(fight => fight.Id == fightId.Value);
            if (explicitFight is null)
            {
                throw new InvalidOperationException($"Fight id {fightId.Value} not found in this report.");
            }

            return (explicitFight.Id, explicitFight.Name, explicitFight.Start, explicitFight.End);
        }

        // Prefer a kill attempt; fallback to the longest pull.
        var kill = list.Where(fight => fight.Kill).OrderByDescending(fight => fight.End - fight.Start).FirstOrDefault();
        var selected = kill ?? list.OrderByDescending(fight => fight.End - fight.Start).First();
        return (selected.Id, selected.Name, selected.Start, selected.End);
    }

    private static bool TryGetReport(JsonElement metadata, out JsonReport report)
    {
        report = default;

        if (!metadata.TryGetProperty("reportData", out var reportData) ||
            !reportData.TryGetProperty("report", out var reportElement) ||
            reportElement.ValueKind == JsonValueKind.Null)
        {
            return false;
        }

        report = new JsonReport(reportElement);
        return true;
    }

    private readonly record struct JsonReport(JsonElement element);
    private readonly record struct RotationEventPage(List<RotationEventPoint> Events, double? NextPageTimestamp);
}
