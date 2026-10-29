namespace Lorrgs.WarcraftLogs.Models;

/// <summary>A spell observed in a report; durations are measured events, not assumed cooldowns.</summary>
public sealed record TimelineSpell(int Id, string Name, string? Icon);
