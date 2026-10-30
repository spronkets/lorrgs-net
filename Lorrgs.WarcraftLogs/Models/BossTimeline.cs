namespace Lorrgs.WarcraftLogs.Models;

/// <summary>Enemy casts with outgoing per-target results, received effects and observed phases from one reference fight, not a universal boss schedule.</summary>
public sealed record BossTimeline(
    string ReportCode, int FightId, string Name, double Duration, bool IsComplete,
    IReadOnlyList<TimelineSpell> Spells, IReadOnlyList<TimelineEvent> Events,
    IReadOnlyList<TimelinePhase> Phases);
