namespace Lorrgs.Api.Models;

/// <summary>A spell observed in a report; durations are measured events, not assumed cooldowns.</summary>
public sealed record TimelineSpell(int Id, string Name, string? Icon);

/// <summary>A cast, buff interval, or death relative to the selected fight start, in milliseconds.</summary>
public sealed record TimelineEvent(int SpellId, double Timestamp, double? Duration, string Kind);

/// <summary>A single player's fight timeline and its explicit pagination completeness.</summary>
public sealed record ReportTimeline(
    string ReportCode, int FightId, int SourceId, string PlayerName,
    double Duration, bool IsComplete, IReadOnlyList<TimelineSpell> Spells,
    IReadOnlyList<TimelineEvent> Events);
