using System.Text.Json;
using Lorrgs.WarcraftLogs.Models;
using Microsoft.Extensions.Logging;
using WclClient = Lorrgs.WarcraftLogs.WarcraftLogsClient;

namespace Lorrgs.WarcraftLogs.Services;

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

    private static readonly string RotationDamageEventsQuery =
            GraphQlResourceLoader.Load("Rotation.RotationDamageEvents.graphql");

    private static readonly string RotationHealingEventsQuery =
            GraphQlResourceLoader.Load("Rotation.RotationHealingEvents.graphql");

    private static readonly string RotationFightRankingsQuery =
            GraphQlResourceLoader.Load("Rotation.RotationFightRankings.graphql");

    public async Task<RotationAnalysisResult> AnalyzeAsync(
        RotationAnalysisOptions request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ReportCode))
        {
            throw new ArgumentException("ReportCode is required.");
        }
        string? notesFromRoleInference = null;

        var metadata = await _warcraftLogsClient.QueryGraphQLAsync(
            ReportMetadataQuery,
            new { code = request.ReportCode.Trim() },
            cancellationToken: cancellationToken
        );

        if (!TryGetReport(metadata, out var report))
        {
            throw new InvalidOperationException("Report not found or inaccessible.");
        }

        var selectedFight = ResolveFight(report, request.FightId);
        var selectedActor = ResolveActor(report, request.SourceId, request.PlayerName);
        var roleHint = InferRole(selectedActor.subType);
        var spellMetadata = BuildSpellMetadata(report);
        _logger.LogInformation("Retrieved rotation report report={ReportCode} title={ReportTitle} fight={FightId} name={FightName} source={SourceId} player={PlayerName}",
            report.element.TryGetProperty("code", out var code) ? code.GetString() : null,
            report.element.TryGetProperty("title", out var title) ? title.GetString() : null,
            selectedFight.id, selectedFight.name, selectedActor.id, selectedActor.name);

        var fightStart = selectedFight.startTime;
        var fightEnd = selectedFight.endTime;
        var eventLimit = Math.Clamp(request.EventLimit, 100, 10000);
        var timeline = await LoadRotationCastTimeline(
            request.ReportCode.Trim(),
            selectedActor.id,
            selectedFight.id,
            fightStart,
            fightEnd,
            eventLimit,
            cancellationToken);
        var damageTimeline = new List<RotationDamagePoint>();
        if (roleHint == "unknown")
        {
            var healingTimeline = await LoadRotationThroughputTimeline(
                request.ReportCode.Trim(),
                selectedActor.id,
                selectedFight.id,
                fightStart,
                fightEnd,
                eventLimit,
                "healer",
                cancellationToken);
            var offensiveTimeline = await LoadRotationThroughputTimeline(
                request.ReportCode.Trim(),
                selectedActor.id,
                selectedFight.id,
                fightStart,
                fightEnd,
                eventLimit,
                "dps",
                cancellationToken);
            var healingTotal = healingTimeline.Sum(point => point.Amount);
            var offensiveTotal = offensiveTimeline.Sum(point => point.Amount);
            if (healingTotal > offensiveTotal * 1.1 && healingTotal > 0)
            {
                roleHint = "healer";
                damageTimeline = healingTimeline;
                notesFromRoleInference = "Detected healer playstyle from throughput profile.";
            }
            else
            {
                roleHint = "dps";
                damageTimeline = offensiveTimeline;
                notesFromRoleInference = "Detected offensive playstyle from throughput profile.";
            }
        }
        else
        {
            damageTimeline = await LoadRotationThroughputTimeline(
                request.ReportCode.Trim(),
                selectedActor.id,
                selectedFight.id,
                fightStart,
                fightEnd,
                eventLimit,
                roleHint,
                cancellationToken);
        }

        timeline = timeline.OrderBy(point => point.Timestamp).ToList();
        foreach (var point in timeline)
        {
            if (point.SpellId is > 0 && spellMetadata.TryGetValue(point.SpellId.Value, out var metadataPoint))
            {
                point.Ability = metadataPoint.Name;
            }
        }
        var abilitySummary = BuildAbilitySummary(timeline);
        var damageSummary = BuildDamageSummary(damageTimeline, spellMetadata);
        var durationSeconds = Math.Max((fightEnd - fightStart) / 1000.0, 1);
        var castsPerMinute = timeline.Count / (durationSeconds / 60.0);
        var phases = BuildFightPhases(selectedFight.phases, durationSeconds);
        var phaseSummaries = BuildPhaseSummaries(timeline, damageTimeline, spellMetadata, phases, fightStart, fightEnd);

        var parsePercentile = request.ParsePercentile;
        if (!parsePercentile.HasValue && request.AutoResolveParsePercentile)
        {
            parsePercentile = await LoadParsePercentile(
                request.ReportCode.Trim(),
                selectedFight.id,
                selectedActor.name,
                selectedActor.className,
                roleHint,
                cancellationToken);
        }

        var (score, notes, verdict) = ScoreRotation(
            castsPerMinute,
            abilitySummary,
            parsePercentile,
            request.ItemLevel,
            roleHint
        );
        if (!string.IsNullOrWhiteSpace(notesFromRoleInference))
        {
            notes.Add(notesFromRoleInference);
        }
        notes.Add($"Observed cast cadence: {Math.Round(castsPerMinute, 2)} casts/min across {Math.Round(durationSeconds, 1)}s.");
        notes.Add($"Tracked unique cast abilities: {abilitySummary.Count}.");
        if (damageSummary.Count > 0)
        {
            var topThroughput = string.Join(", ", damageSummary.Take(3)
                .Select(row => $"{row.Ability} ({row.SharePercent}%)"));
            var throughputLabel = roleHint == "healer" ? "healing" : "damage";
            notes.Add($"Top {throughputLabel} contributors: {topThroughput}.");
        }
        if (phaseSummaries.Count > 1)
        {
            notes.Add($"Phase-aware rotation summary generated across {phaseSummaries.Count} phases.");
            var phaseMin = phaseSummaries.Min(phase => phase.CastsPerMinute);
            var phaseMax = phaseSummaries.Max(phase => phase.CastsPerMinute);
            notes.Add($"Phase cast cadence range: {phaseMin} to {phaseMax} casts/min.");
        }

        return new RotationAnalysisResult
        {
            ReportCode = request.ReportCode.Trim(),
            FightId = selectedFight.id,
            FightName = selectedFight.name,
            PlayerName = selectedActor.name,
            SourceId = selectedActor.id,
            SpecHint = selectedActor.subType,
            RoleHint = roleHint,
            GameExpansionHint = TryGetNestedString(report.element, "zone", "name"),
            ParsePercentile = parsePercentile,
            ItemLevel = request.ItemLevel,
            FightDurationSeconds = durationSeconds,
            CastsPerMinute = Math.Round(castsPerMinute, 2),
            Verdict = verdict,
            Score = score,
            Notes = notes,
            AbilitySummary = abilitySummary,
            DamageSummary = damageSummary,
            RotationTimeline = timeline.Take(200).ToList(),
            Phases = phases,
            PhaseSummaries = phaseSummaries,
            SpellMetadata = spellMetadata.Values.OrderBy(spell => spell.SpellId).ToList()
        };
    }

    private static (int score, List<string> notes, string verdict) ScoreRotation(
        double castsPerMinute,
        List<RotationAbilitySummary> abilitySummary,
        int? parsePercentile,
        int? itemLevel,
        string roleHint)
    {
        var score = 40;
        var notes = new List<string>();

        if (castsPerMinute >= 25)
        {
            score += 20;
            notes.Add("Cast cadence cleared the high-activity threshold (>=25 casts/min).");
        }
        else if (castsPerMinute >= 18)
        {
            score += 10;
            notes.Add("Cast cadence cleared the baseline activity threshold (>=18 casts/min).");
        }
        else
        {
            notes.Add("Cast cadence was below the baseline activity threshold (18 casts/min).");
        }

        var distinctAbilities = abilitySummary.Count;
        if (distinctAbilities >= 8)
        {
            score += 10;
            notes.Add("Ability diversity bonus applied (>=8 distinct casts).");
        }
        else if (distinctAbilities <= 3)
        {
            notes.Add("Ability diversity penalty warning (<=3 distinct casts).");
        }

        if (parsePercentile.HasValue)
        {
            if (parsePercentile.Value >= 85)
            {
                score += 20;
                notes.Add($"Parse percentile bonus applied ({parsePercentile.Value}th percentile).");
            }
            else if (parsePercentile.Value >= 60)
            {
                score += 10;
                notes.Add($"Parse percentile baseline bonus applied ({parsePercentile.Value}th percentile).");
            }
            else
            {
                notes.Add($"Parse percentile below baseline ({parsePercentile.Value}th percentile).");
            }
        }
        else
        {
            score = Math.Min(score, 65);
            notes.Add("No parse percentile supplied; score is heuristic-only and capped.");
        }

        if (itemLevel.HasValue)
        {
            if (itemLevel.Value < 500 && (parsePercentile ?? 0) >= 70)
            {
                score += 8;
                notes.Add("Low-ilvl efficiency bonus applied (ilvl < 500 with >=70 parse).");
            }
            else if (itemLevel.Value >= 520 && (parsePercentile ?? 0) < 50)
            {
                notes.Add("High-ilvl underperformance warning triggered (ilvl >= 520 with <50 parse).");
            }
        }

        score = Math.Clamp(score, 0, 100);
        var verdict = parsePercentile.HasValue
            ? score >= 70 ? "optimal" : "needs_improvement"
            : "needs_context";
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

        if (spec is "warrior" or "paladin" or "hunter" or "rogue" or "priest" or "death knight" or "shaman" or "mage" or "warlock" or "monk" or "druid" or "demon hunter" or "evoker")
        {
            return "unknown";
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
            .GroupBy(point => (point.Ability, point.SpellId))
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
                    Ability = group.Key.Ability,
                    SpellId = group.Key.SpellId,
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

    private async Task<List<RotationEventPoint>> LoadRotationCastTimeline(
        string reportCode,
        int sourceId,
        int fightId,
        long fightStart,
        long fightEnd,
        int eventLimit,
        CancellationToken cancellationToken)
    {
        var timeline = new List<RotationEventPoint>();
        var pageStart = fightStart;

        while (pageStart < fightEnd && timeline.Count < eventLimit)
        {
            var pagePayload = await _warcraftLogsClient.QueryGraphQLAsync(
                RotationEventsQuery,
                new
                {
                    code = reportCode,
                    startTime = pageStart,
                    endTime = fightEnd,
                    sourceId,
                    fightIds = new[] { fightId },
                    limit = eventLimit
                },
                cancellationToken: cancellationToken
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

        return timeline;
    }

    private async Task<List<RotationDamagePoint>> LoadRotationThroughputTimeline(
        string reportCode,
        int sourceId,
        int fightId,
        long fightStart,
        long fightEnd,
        int eventLimit,
        string roleHint,
        CancellationToken cancellationToken)
    {
        var timeline = new List<RotationDamagePoint>();
        var pageStart = fightStart;
        var query = roleHint == "healer" ? RotationHealingEventsQuery : RotationDamageEventsQuery;

        while (pageStart < fightEnd && timeline.Count < eventLimit)
        {
            var pagePayload = await _warcraftLogsClient.QueryGraphQLAsync(
                query,
                new
                {
                    code = reportCode,
                    startTime = pageStart,
                    endTime = fightEnd,
                    sourceId,
                    fightIds = new[] { fightId },
                    limit = eventLimit
                },
                cancellationToken: cancellationToken
            );

            var page = ParseRotationDamageEvents(pagePayload);
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

        return timeline;
    }

    private static IReadOnlyDictionary<int, RotationSpellMetadata> BuildSpellMetadata(JsonReport report)
    {
        if (!report.element.TryGetProperty("masterData", out var masterData) ||
            !masterData.TryGetProperty("abilities", out var abilities) ||
            abilities.ValueKind != JsonValueKind.Array)
        {
            return new Dictionary<int, RotationSpellMetadata>();
        }

        return abilities.EnumerateArray()
            .Where(item =>
                item.TryGetProperty("gameID", out var gameIdNode) &&
                gameIdNode.ValueKind == JsonValueKind.Number &&
                gameIdNode.TryGetInt32(out var gameId) &&
                gameId > 0)
            .Select(item =>
            {
                var spellId = item.GetProperty("gameID").GetInt32();
                var name = item.TryGetProperty("name", out var nameNode) ? nameNode.GetString() : null;
                var icon = item.TryGetProperty("icon", out var iconNode) ? iconNode.GetString() : null;
                return new RotationSpellMetadata
                {
                    SpellId = spellId,
                    Name = string.IsNullOrWhiteSpace(name) ? $"Spell {spellId}" : name.Trim(),
                    Icon = string.IsNullOrWhiteSpace(icon) ? null : icon.Trim()
                };
            })
            .GroupBy(spell => spell.SpellId)
            .Select(group => group.First())
            .ToDictionary(spell => spell.SpellId);
    }

    private static List<RotationDamageSummary> BuildDamageSummary(
        List<RotationDamagePoint> timeline,
        IReadOnlyDictionary<int, RotationSpellMetadata> spellMetadata)
    {
        var totalDamage = Math.Max(timeline.Sum(point => point.Amount), 1);
        return timeline
            .GroupBy(point => point.SpellId)
            .Select(group => new RotationDamageSummary
            {
                Ability = spellMetadata.TryGetValue(group.Key, out var metadata)
                    ? metadata.Name
                    : group.Select(point => point.Ability).FirstOrDefault() ?? $"Spell:{group.Key}",
                SpellId = group.Key,
                TotalDamage = Math.Round(group.Sum(point => point.Amount), 2),
                HitCount = group.Count(),
                SharePercent = Math.Round(group.Sum(point => point.Amount) * 100.0 / totalDamage, 2)
            })
            .OrderByDescending(summary => summary.TotalDamage)
            .Take(20)
            .ToList();
    }

    private static List<TimelinePhase> BuildFightPhases(IReadOnlyList<TimelinePhase> phases, double durationSeconds)
    {
        var durationMs = Math.Max(durationSeconds * 1000.0, 1);
        var normalized = phases
            .Where(phase => !phase.IsIntermission && phase.Timestamp >= 0 && phase.Timestamp <= durationMs)
            .OrderBy(phase => phase.Timestamp)
            .Select(phase => new TimelinePhase(
                phase.NotePhaseId ?? checked((int)Math.Round(phase.Id)),
                phase.Timestamp,
                false,
                phase.NotePhaseId))
            .GroupBy(phase => Math.Round(phase.Timestamp, 3))
            .Select(group => group.First())
            .ToList();

        if (normalized.Count == 0 || normalized[0].Timestamp > 0)
        {
            normalized.Insert(0, new TimelinePhase(1, 0));
        }

        return normalized;
    }

    private static List<RotationPhaseSummary> BuildPhaseSummaries(
        List<RotationEventPoint> timeline,
        List<RotationDamagePoint> damageTimeline,
        IReadOnlyDictionary<int, RotationSpellMetadata> spellMetadata,
        IReadOnlyList<TimelinePhase> phases,
        long fightStart,
        long fightEnd)
    {
        var durationMs = Math.Max(fightEnd - fightStart, 1);
        var summaries = new List<RotationPhaseSummary>();

        for (var index = 0; index < phases.Count; index++)
        {
            var phase = phases[index];
            var phaseStart = Math.Clamp(phase.Timestamp, 0, durationMs);
            var phaseEnd = index + 1 < phases.Count
                ? Math.Clamp(phases[index + 1].Timestamp, phaseStart, durationMs)
                : durationMs;

            if (phaseEnd <= phaseStart)
            {
                continue;
            }

            var phaseEvents = timeline
                .Where(point =>
                {
                    var relative = point.Timestamp - fightStart;
                    return relative >= phaseStart && relative < phaseEnd;
                })
                .ToList();
            var durationSeconds = Math.Max((phaseEnd - phaseStart) / 1000.0, 1);
            var abilitySummary = BuildAbilitySummary(phaseEvents).Take(5).ToList();
            var phaseDamageEvents = damageTimeline
                .Where(point =>
                {
                    var relative = point.Timestamp - fightStart;
                    return relative >= phaseStart && relative < phaseEnd;
                })
                .ToList();
            var phaseDamageSummary = BuildDamageSummary(phaseDamageEvents, spellMetadata).Take(5).ToList();
            summaries.Add(new RotationPhaseSummary
            {
                PhaseId = checked((int)Math.Round(phase.Id)),
                StartSeconds = Math.Round(phaseStart / 1000.0, 1),
                EndSeconds = Math.Round(phaseEnd / 1000.0, 1),
                DurationSeconds = Math.Round(durationSeconds, 1),
                CastCount = phaseEvents.Count,
                CastsPerMinute = Math.Round(phaseEvents.Count / (durationSeconds / 60.0), 2),
                TotalDamage = Math.Round(phaseDamageEvents.Sum(point => point.Amount), 2),
                RotationPattern = BuildRotationPattern(phaseEvents, spellMetadata),
                TopAbilities = abilitySummary,
                CooldownSpellIds = DetectCooldownSpellIds(abilitySummary),
                TopDamageAbilities = phaseDamageSummary
            });
        }

        return summaries;
    }

    private static string BuildRotationPattern(
        List<RotationEventPoint> phaseEvents,
        IReadOnlyDictionary<int, RotationSpellMetadata> spellMetadata)
    {
        if (phaseEvents.Count == 0)
        {
            return "No casts recorded in this phase.";
        }

        var ordered = phaseEvents.OrderBy(point => point.Timestamp).ToList();
        var intervals = new List<double>();
        for (var i = 1; i < ordered.Count; i++)
        {
            intervals.Add((ordered[i].Timestamp - ordered[i - 1].Timestamp) / 1000.0);
        }

        var medianInterval = intervals.Count > 0 ? Median(intervals) : 0;
        var waitThreshold = Math.Max(2.5, medianInterval * 1.8);
        var steps = new List<string>();
        string? previousStep = null;
        long? previousTimestamp = null;

        foreach (var point in ordered.Take(18))
        {
            if (previousTimestamp.HasValue)
            {
                var gapSeconds = (point.Timestamp - previousTimestamp.Value) / 1000.0;
                if (gapSeconds >= waitThreshold)
                {
                    steps.Add("wait");
                    previousStep = "wait";
                }
            }

            var label = DescribeSpell(point, spellMetadata);
            if (string.Equals(previousStep, label, StringComparison.Ordinal))
            {
                previousTimestamp = point.Timestamp;
                continue;
            }

            steps.Add(label);
            previousStep = label;
            previousTimestamp = point.Timestamp;
        }

        if (steps.Count == 0)
        {
            return "No cast sequence detected.";
        }

        var sequence = string.Join(" → ", steps);
        if (ordered.Count > 18)
        {
            sequence += " → …";
        }

        return sequence;
    }

    private static double Median(List<double> values)
    {
        values.Sort();
        var middle = values.Count / 2;
        return values.Count % 2 == 0
            ? (values[middle - 1] + values[middle]) / 2.0
            : values[middle];
    }

    private static string DescribeSpell(
        RotationEventPoint point,
        IReadOnlyDictionary<int, RotationSpellMetadata> spellMetadata)
    {
        if (point.SpellId is > 0 && spellMetadata.TryGetValue(point.SpellId.Value, out var metadata))
        {
            return metadata.Name;
        }

        if (point.SpellId is > 0)
        {
            return $"spell #{point.SpellId.Value}";
        }

        if (string.IsNullOrWhiteSpace(point.Ability))
        {
            return "unknown";
        }

        return point.Ability.StartsWith("Spell:", StringComparison.OrdinalIgnoreCase)
            ? point.Ability.Replace(":", " #", StringComparison.OrdinalIgnoreCase)
            : point.Ability;
    }

    private static List<int> DetectCooldownSpellIds(List<RotationAbilitySummary> abilitySummary)
    {
        return abilitySummary
            .Where(summary =>
                summary.SpellId is > 0 &&
                (summary.CastCount <= 3 || summary.MedianIntervalSeconds is >= 25))
            .Select(summary => summary.SpellId!.Value)
            .Distinct()
            .ToList();
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
            if (!item.TryGetProperty("abilityGameID", out var abilityIdNode) ||
                abilityIdNode.ValueKind != JsonValueKind.Number ||
                !abilityIdNode.TryGetInt32(out var abilityId) ||
                !item.TryGetProperty("timestamp", out var timestampElement))
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
                ability = $"Spell:{abilityId}";
            }

            timeline.Add(new RotationEventPoint
            {
                Timestamp = timestampElement.GetInt64(),
                Ability = ability,
                SpellId = abilityId,
                EventType = item.TryGetProperty("type", out var typeNode) ? typeNode.GetString() : "cast"
            });
        }

        return new RotationEventPage(timeline, nextPageTimestamp);
    }

    private static RotationDamagePage ParseRotationDamageEvents(JsonElement eventsPayload)
    {
        var timeline = new List<RotationDamagePoint>();
        double? nextPageTimestamp = null;

        if (!eventsPayload.TryGetProperty("reportData", out var reportData) ||
            !reportData.TryGetProperty("report", out var report) ||
            !report.TryGetProperty("events", out var eventsNode) ||
            !eventsNode.TryGetProperty("data", out var eventsData) ||
            eventsData.ValueKind != JsonValueKind.Array)
        {
            return new RotationDamagePage(timeline, nextPageTimestamp);
        }

        if (eventsNode.TryGetProperty("nextPageTimestamp", out var nextPageNode) &&
            nextPageNode.ValueKind == JsonValueKind.Number &&
            nextPageNode.TryGetDouble(out var nextPageValue))
        {
            nextPageTimestamp = nextPageValue;
        }

        foreach (var item in eventsData.EnumerateArray())
        {
            if (!item.TryGetProperty("abilityGameID", out var abilityIdNode) ||
                abilityIdNode.ValueKind != JsonValueKind.Number ||
                !abilityIdNode.TryGetInt32(out var abilityId) ||
                !item.TryGetProperty("timestamp", out var timestampElement) ||
                !item.TryGetProperty("amount", out var amountNode) ||
                amountNode.ValueKind != JsonValueKind.Number)
            {
                continue;
            }

            string ability;
            if (item.TryGetProperty("ability", out var abilityNode) && abilityNode.TryGetProperty("name", out var abilityName))
            {
                ability = abilityName.GetString() ?? $"Spell:{abilityId}";
            }
            else
            {
                ability = $"Spell:{abilityId}";
            }

            timeline.Add(new RotationDamagePoint(
                timestampElement.GetInt64(),
                ability,
                abilityId,
                amountNode.GetDouble()));
        }

        return new RotationDamagePage(timeline, nextPageTimestamp);
    }

    private async Task<int?> LoadParsePercentile(
        string reportCode,
        int fightId,
        string actorName,
        string actorClass,
        string roleHint,
        CancellationToken cancellationToken)
    {
        var rankingsPayload = await _warcraftLogsClient.QueryGraphQLAsync(
            RotationFightRankingsQuery,
            new
            {
                code = reportCode,
                fightIds = new[] { fightId }
            },
            cancellationToken: cancellationToken
        );

        if (!rankingsPayload.TryGetProperty("reportData", out var reportData) ||
            !reportData.TryGetProperty("report", out var reportElement))
        {
            return null;
        }

        var rankingFields = roleHint switch
        {
            "healer" => new[] { "healingRankings", "damageRankings" },
            "tank" => new[] { "damageRankings", "healingRankings" },
            _ => new[] { "damageRankings", "healingRankings" }
        };

        var roleOrder = roleHint switch
        {
            "healer" => new[] { "healers", "dps", "tanks" },
            "tank" => new[] { "tanks", "dps", "healers" },
            _ => new[] { "dps", "tanks", "healers" }
        };

        var matches = new List<double>();
        foreach (var rankingField in rankingFields)
        {
            if (!reportElement.TryGetProperty(rankingField, out var rankingsElement) ||
                rankingsElement.ValueKind != JsonValueKind.Object ||
                !rankingsElement.TryGetProperty("data", out var dataElement) ||
                dataElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var fightElement in dataElement.EnumerateArray())
            {
                if (!fightElement.TryGetProperty("fightID", out var fightIdNode) ||
                    fightIdNode.ValueKind != JsonValueKind.Number ||
                    fightIdNode.GetInt32() != fightId ||
                    !fightElement.TryGetProperty("roles", out var rolesElement))
                {
                    continue;
                }

                foreach (var role in roleOrder)
                {
                    if (!rolesElement.TryGetProperty(role, out var roleElement) ||
                        !roleElement.TryGetProperty("characters", out var charactersElement) ||
                        charactersElement.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var character in charactersElement.EnumerateArray())
                    {
                        if (!character.TryGetProperty("name", out var nameElement) ||
                            !string.Equals(nameElement.GetString(), actorName, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var classMatches = !character.TryGetProperty("class", out var classElement) ||
                            string.IsNullOrWhiteSpace(actorClass) ||
                            string.Equals(actorClass, "Player", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(classElement.GetString(), actorClass, StringComparison.OrdinalIgnoreCase);
                        if (!classMatches)
                        {
                            continue;
                        }

                        if (character.TryGetProperty("rankPercent", out var rankPercentElement) &&
                            rankPercentElement.ValueKind == JsonValueKind.Number &&
                            rankPercentElement.TryGetDouble(out var value) &&
                            value is >= 0 and <= 100)
                        {
                            matches.Add(value);
                        }
                    }
                }
            }
        }

        if (matches.Count == 0)
        {
            _logger.LogInformation(
                "No parse percentile match found report={ReportCode} fight={FightId} actor={ActorName} class={ActorClass} role={RoleHint}",
                reportCode, fightId, actorName, actorClass, roleHint);
            return null;
        }

        var parse = (int)Math.Round(matches.Max(), MidpointRounding.AwayFromZero);
        _logger.LogInformation(
            "Resolved parse percentile report={ReportCode} fight={FightId} actor={ActorName} role={RoleHint} percentile={ParsePercentile}",
            reportCode, fightId, actorName, roleHint, parse);
        return parse;
    }

    private static (int id, string name, string subType, string className) ResolveActor(JsonReport report, int? sourceId, string? playerName)
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
                SubType = actor.TryGetProperty("subType", out var subTypeNode) ? subTypeNode.GetString() ?? "Unknown" : "Unknown",
                ClassName = actor.TryGetProperty("type", out var classNode) ? classNode.GetString() ?? string.Empty : string.Empty
            })
            .ToList();

        if (sourceId.HasValue)
        {
            var byId = allActors.FirstOrDefault(actor => actor.Id == sourceId.Value);
            if (byId is null)
            {
                throw new InvalidOperationException($"Player sourceId {sourceId.Value} not found in this report.");
            }

            return (byId.Id, byId.Name, byId.SubType, byId.ClassName);
        }

        if (!string.IsNullOrWhiteSpace(playerName))
        {
            var byName = allActors.FirstOrDefault(actor => string.Equals(actor.Name, playerName, StringComparison.OrdinalIgnoreCase));
            if (byName is null)
            {
                throw new InvalidOperationException($"Player '{playerName}' not found in this report.");
            }

            return (byName.Id, byName.Name, byName.SubType, byName.ClassName);
        }

        var fallback = allActors.FirstOrDefault();
        if (fallback is null)
        {
            throw new InvalidOperationException("No players found in this report.");
        }

        return (fallback.Id, fallback.Name, fallback.SubType, fallback.ClassName);
    }

    private static (int id, string name, long startTime, long endTime, IReadOnlyList<TimelinePhase> phases) ResolveFight(JsonReport report, int? fightId)
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
                Kill = fight.TryGetProperty("kill", out var killNode) &&
                    (killNode.ValueKind == JsonValueKind.True || killNode.ValueKind == JsonValueKind.False) &&
                    killNode.GetBoolean(),
                Phases = ObservedPhases(fight)
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

            return (explicitFight.Id, explicitFight.Name, explicitFight.Start, explicitFight.End, explicitFight.Phases);
        }

        // Prefer a kill attempt; fallback to the longest pull.
        var kill = list.Where(fight => fight.Kill).OrderByDescending(fight => fight.End - fight.Start).FirstOrDefault();
        var selected = kill ?? list.OrderByDescending(fight => fight.End - fight.Start).First();
        return (selected.Id, selected.Name, selected.Start, selected.End, selected.Phases);
    }

    private static IReadOnlyList<TimelinePhase> ObservedPhases(JsonElement fight)
    {
        if (!fight.TryGetProperty("phaseTransitions", out var transitions) || transitions.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var start = fight.TryGetProperty("startTime", out var startTimeNode) && startTimeNode.ValueKind == JsonValueKind.Number
            ? startTimeNode.GetDouble()
            : 0;
        var end = fight.TryGetProperty("endTime", out var endTimeNode) && endTimeNode.ValueKind == JsonValueKind.Number
            ? endTimeNode.GetDouble()
            : start;
        if (end <= start)
        {
            return [];
        }

        return transitions.EnumerateArray()
            .Where(item =>
                item.TryGetProperty("id", out var idNode) &&
                idNode.ValueKind == JsonValueKind.Number &&
                item.TryGetProperty("startTime", out var transitionNode) &&
                transitionNode.ValueKind == JsonValueKind.Number &&
                transitionNode.GetDouble() >= start &&
                transitionNode.GetDouble() <= end)
            .Select(item => new TimelinePhase(
                checked((int)item.GetProperty("id").GetDouble()),
                item.GetProperty("startTime").GetDouble() - start))
            .OrderBy(phase => phase.Timestamp)
            .ToArray();
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
    private readonly record struct RotationDamagePage(List<RotationDamagePoint> Events, double? NextPageTimestamp);
    private readonly record struct RotationDamagePoint(long Timestamp, string Ability, int SpellId, double Amount);
}
