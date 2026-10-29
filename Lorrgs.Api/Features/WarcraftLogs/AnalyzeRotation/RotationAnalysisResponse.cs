using Lorrgs.WarcraftLogs.Models;

namespace Lorrgs.Api.Features.WarcraftLogs.AnalyzeRotation;

/// <summary>Summarizes a player's rotation and observed ability usage.</summary>
public sealed record RotationAnalysisResponse
{
    public required string ReportCode { get; init; }
    public int FightId { get; init; }
    public required string FightName { get; init; }
    public required string PlayerName { get; init; }
    public int SourceId { get; init; }
    public required string RoleHint { get; init; }
    public required string SpecHint { get; init; }
    public string? GameExpansionHint { get; init; }
    public int? ParsePercentile { get; init; }
    public int? ItemLevel { get; init; }
    public double FightDurationSeconds { get; init; }
    public double CastsPerMinute { get; init; }
    public required string Verdict { get; init; }
    public int Score { get; init; }
    public required List<string> Notes { get; init; }
    public required List<RotationAbilitySummary> AbilitySummary { get; init; }
    public required List<RotationEventPoint> RotationTimeline { get; init; }
}
