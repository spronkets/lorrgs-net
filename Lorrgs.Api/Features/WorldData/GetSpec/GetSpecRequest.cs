using Microsoft.AspNetCore.Mvc;

namespace Lorrgs.Api.Features.WorldData.GetSpec;

/// <summary>Parameters for GetSpec.</summary>
public sealed record GetSpecRequest
{
    [FromRoute(Name = "specSlug")]
    public string SpecSlug { get; init; } = "";

    [FromQuery(Name = "edition")]
    public string? Edition { get; init; }
}
