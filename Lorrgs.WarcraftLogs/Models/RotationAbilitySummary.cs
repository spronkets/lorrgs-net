namespace Lorrgs.WarcraftLogs.Models;

public class RotationAbilitySummary
{
    public required string Ability { get; set; }
    public int? SpellId { get; set; }
    public int CastCount { get; set; }
    public double SharePercent { get; set; }
    public double? MedianIntervalSeconds { get; set; }
}
