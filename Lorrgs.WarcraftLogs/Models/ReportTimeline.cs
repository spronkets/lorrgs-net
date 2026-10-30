namespace Lorrgs.WarcraftLogs.Models;

/// <summary>A single player's fight timeline and its explicit pagination completeness.</summary>
public sealed record ReportTimeline(
    string ReportCode, int FightId, int SourceId, string PlayerName,
    double Duration, bool IsComplete, IReadOnlyList<TimelineSpell> Spells,
    IReadOnlyList<TimelineEvent> Events)
{
    public double? MaxHealth { get; init; }
    public double? EstimatedMaxHealth { get; init; }
    public IReadOnlyList<TimelineHealthCapacity> HealthCapacities { get; init; } = [];
    public double? MaxMana { get; init; }
}
