namespace Lorrgs.WarcraftLogs.Models;

/// <summary>Enemy casts and observed phases from one reference fight, not a universal boss schedule.</summary>
public sealed record BossTimeline(
    string ReportCode, int FightId, string Name, double Duration, bool IsComplete,
    IReadOnlyList<TimelineSpell> Spells, IReadOnlyList<TimelineEvent> Events,
    IReadOnlyList<TimelinePhase> Phases);
