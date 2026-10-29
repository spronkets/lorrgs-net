using Microsoft.AspNetCore.Mvc;

namespace Lorrgs.Api.Features.WorldData.GetClassSpecs;

/// <summary>Parameters for GetClassSpecs.</summary>
public sealed record GetClassSpecsRequest
{
    [FromRoute(Name = "classSlug")]
    public string ClassSlug { get; init; } = "";

    [FromQuery(Name = "edition")]
    public string? Edition { get; init; }
}
