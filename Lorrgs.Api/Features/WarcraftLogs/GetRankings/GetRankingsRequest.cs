using Microsoft.AspNetCore.Mvc;

namespace Lorrgs.Api.Features.WarcraftLogs.GetRankings;

/// <summary>Parameters for GetRankings.</summary>
public sealed record GetRankingsRequest(
    [property: FromQuery(Name = "season")] string? Season = null);
