using Microsoft.AspNetCore.Mvc;

namespace Lorrgs.Api.Features.WorldData.GetBoss;

/// <summary>Parameters for GetBoss.</summary>
public sealed record GetBossRequest
{
    [FromRoute(Name = "bossSlug")]
    public string BossSlug { get; init; } = "";

    [FromQuery(Name = "edition")]
    public string? Edition { get; init; }
}
