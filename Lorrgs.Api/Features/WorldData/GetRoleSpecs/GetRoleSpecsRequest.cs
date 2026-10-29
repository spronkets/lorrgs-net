using Microsoft.AspNetCore.Mvc;

namespace Lorrgs.Api.Features.WorldData.GetRoleSpecs;

/// <summary>Parameters for GetRoleSpecs.</summary>
public sealed record GetRoleSpecsRequest
{
    [FromRoute(Name = "roleSlug")]
    public string RoleSlug { get; init; } = "";

    [FromQuery(Name = "edition")]
    public string? Edition { get; init; }
}
