using Microsoft.AspNetCore.Mvc;

namespace Lorrgs.Api.Features.WorldData.GetBossSpells;

/// <summary>Parameters for GetBossSpells.</summary>
public sealed record GetBossSpellsRequest
{
    [FromRoute(Name = "bossSlug")]
    public string BossSlug { get; init; } = "";

    [FromQuery(Name = "edition")]
    public string? Edition { get; init; }
}
