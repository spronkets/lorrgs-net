using Microsoft.AspNetCore.Mvc;

namespace Lorrgs.Api.Features.WorldData.GetSpecSpells;

/// <summary>Parameters for GetSpecSpells.</summary>
public sealed record GetSpecSpellsRequest
{
    [FromRoute(Name = "specSlug")]
    public string SpecSlug { get; init; } = "";

    [FromQuery(Name = "edition")]
    public string? Edition { get; init; }
}
