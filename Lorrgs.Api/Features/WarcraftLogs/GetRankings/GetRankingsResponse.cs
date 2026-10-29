namespace Lorrgs.Api.Features.WarcraftLogs.GetRankings;

/// <summary>A player ranking in the selected season.</summary>
public sealed record GetRankingsResponse(
    int Id, string Name, string ClassName, string Spec, double Percentile, string Season);
