using Microsoft.AspNetCore.Mvc;

namespace Lorrgs.Api.Features.WorldData.GetTrinket;

/// <summary>Parameters for GetTrinket.</summary>
public sealed record GetTrinketRequest
{
    [FromRoute(Name = "trinketId")]
    public int TrinketId { get; init; }

    [FromQuery(Name = "edition")]
    public string? Edition { get; init; }
}
