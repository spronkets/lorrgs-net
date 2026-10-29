using Microsoft.AspNetCore.Mvc;

namespace Lorrgs.Api.Features.WorldData.GetZoneBosses;

/// <summary>Parameters for GetZoneBosses.</summary>
public sealed record GetZoneBossesRequest
{
    [FromRoute(Name = "zoneSlug")]
    public string ZoneSlug { get; init; } = "";

    [FromQuery(Name = "edition")]
    public string? Edition { get; init; }
}
