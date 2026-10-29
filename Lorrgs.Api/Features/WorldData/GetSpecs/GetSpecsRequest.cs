using Microsoft.AspNetCore.Mvc;

namespace Lorrgs.Api.Features.WorldData.GetSpecs;

/// <summary>Parameters for GetSpecs.</summary>
public sealed record GetSpecsRequest
{
    [FromQuery(Name = "roleId")]
    public int? RoleId { get; init; }

    [FromQuery(Name = "classId")]
    public int? ClassId { get; init; }

    [FromQuery(Name = "edition")]
    public string? Edition { get; init; }
}
