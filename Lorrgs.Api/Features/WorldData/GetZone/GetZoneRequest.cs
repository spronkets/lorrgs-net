using Microsoft.AspNetCore.Mvc;

namespace Lorrgs.Api.Features.WorldData.GetZone;

/// <summary>Parameters for GetZone.</summary>
public sealed record GetZoneRequest
{
    [FromRoute(Name = "zoneSlug")]
    public string ZoneSlug { get; init; } = "";

    [FromQuery(Name = "edition")]
    public string? Edition { get; init; }
}
