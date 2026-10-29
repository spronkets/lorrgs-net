namespace Lorrgs.WarcraftLogs.Models;

/// <summary>A single player's fight timeline and its explicit pagination completeness.</summary>
public sealed record ReportTimeline(
    string ReportCode, int FightId, int SourceId, string PlayerName,
    double Duration, bool IsComplete, IReadOnlyList<TimelineSpell> Spells,
    IReadOnlyList<TimelineEvent> Events);
