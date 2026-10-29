namespace Lorrgs.Api.Features.WarcraftLogs.AnalyzeRotation;

/// <summary>Identifies the report, fight and player to analyze.</summary>
public sealed record RotationAnalysisRequest
{
    public required string ReportCode { get; init; }
    public int? FightId { get; init; }
    public int? SourceId { get; init; }
    public string? PlayerName { get; init; }
    public int? ParsePercentile { get; init; }
    public int? ItemLevel { get; init; }
    public int EventLimit { get; init; } = 5000;
}
