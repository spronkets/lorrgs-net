namespace Lorrgs.Api.Models;

/// <summary>A spell observed in a report; durations are measured events, not assumed cooldowns.</summary>
public sealed record TimelineSpell(int Id, string Name, string? Icon);

/// <summary>A cast, buff interval, death, or inferred marker relative to fight start, in milliseconds.</summary>
public sealed record TimelineEvent(int SpellId, double Timestamp, double? Duration, string Kind,
    bool IsInferred = false);

/// <summary>A single player's fight timeline and its explicit pagination completeness.</summary>
public sealed record ReportTimeline(
    string ReportCode, int FightId, int SourceId, string PlayerName,
    double Duration, bool IsComplete, IReadOnlyList<TimelineSpell> Spells,
    IReadOnlyList<TimelineEvent> Events);

/// <summary>An encounter phase or intermission; NotePhaseId preserves the observed addon timer anchor.</summary>
public sealed record TimelinePhase(double Id, double Timestamp, bool IsIntermission = false,
    int? NotePhaseId = null);

/// <summary>Enemy casts and observed phases from one reference fight, not a universal boss schedule.</summary>
public sealed record BossTimeline(
    string ReportCode, int FightId, string Name, double Duration, bool IsComplete,
    IReadOnlyList<TimelineSpell> Spells, IReadOnlyList<TimelineEvent> Events,
    IReadOnlyList<TimelinePhase> Phases);
