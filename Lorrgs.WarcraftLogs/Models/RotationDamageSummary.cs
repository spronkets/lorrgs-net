namespace Lorrgs.WarcraftLogs.Models;

public sealed class RotationDamageSummary
{
    public required string Ability { get; init; }
    public int SpellId { get; init; }
    public double TotalDamage { get; init; }
    public int HitCount { get; init; }
    public double SharePercent { get; init; }
}
