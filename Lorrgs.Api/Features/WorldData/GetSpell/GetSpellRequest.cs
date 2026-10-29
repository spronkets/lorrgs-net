using Microsoft.AspNetCore.Mvc;

namespace Lorrgs.Api.Features.WorldData.GetSpell;

/// <summary>Parameters for GetSpell.</summary>
public sealed record GetSpellRequest
{
    [FromRoute(Name = "spellId")]
    public int SpellId { get; init; }

    [FromQuery(Name = "edition")]
    public string? Edition { get; init; }
}
