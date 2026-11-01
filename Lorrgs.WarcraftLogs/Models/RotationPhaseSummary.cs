namespace Lorrgs.WarcraftLogs.Models;

public sealed class RotationPhaseSummary
{
    public int PhaseId { get; init; }
    public double StartSeconds { get; init; }
    public double EndSeconds { get; init; }
    public double DurationSeconds { get; init; }
    public int CastCount { get; init; }
    public double CastsPerMinute { get; init; }
    public double TotalDamage { get; init; }
    public required string RotationPattern { get; init; }
    public required List<RotationAbilitySummary> TopAbilities { get; init; }
    public required List<int> CooldownSpellIds { get; init; }
    public required List<RotationDamageSummary> TopDamageAbilities { get; init; }
}
