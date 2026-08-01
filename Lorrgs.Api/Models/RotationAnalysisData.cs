namespace Lorrgs.Api.Models;

public class RotationAnalysisRequest
{
    public required string ReportCode { get; set; }
    public int? FightId { get; set; }
    public int? SourceId { get; set; }
    public string? PlayerName { get; set; }
    public int? ParsePercentile { get; set; }
    public int? ItemLevel { get; set; }
    public int EventLimit { get; set; } = 5000;
}

public class RotationAnalysisResponse
{
    public required string ReportCode { get; set; }
    public int FightId { get; set; }
    public required string FightName { get; set; }
    public required string PlayerName { get; set; }
    public int SourceId { get; set; }
    public required string RoleHint { get; set; }
    public required string SpecHint { get; set; }
    public string? GameExpansionHint { get; set; }
    public int? ParsePercentile { get; set; }
    public int? ItemLevel { get; set; }
    public double FightDurationSeconds { get; set; }
    public double CastsPerMinute { get; set; }
    public required string Verdict { get; set; }
    public int Score { get; set; }
    public required List<string> Notes { get; set; }
    public required List<RotationAbilitySummary> AbilitySummary { get; set; }
    public required List<RotationEventPoint> RotationTimeline { get; set; }
}

public class RotationAbilitySummary
{
    public required string Ability { get; set; }
    public int CastCount { get; set; }
    public double SharePercent { get; set; }
    public double? MedianIntervalSeconds { get; set; }
}

public class RotationEventPoint
{
    public long Timestamp { get; set; }
    public required string Ability { get; set; }
    public string? EventType { get; set; }
}
