using Microsoft.AspNetCore.Mvc;

namespace Lorrgs.Api.Features.WorldData.GetSeason;

/// <summary>Parameters for GetSeason.</summary>
public sealed record GetSeasonRequest
{
    [FromRoute(Name = "seasonSlug")]
    public string SeasonSlug { get; init; } = "";

    [FromQuery(Name = "edition")]
    public string? Edition { get; init; }
}
