using Lorrgs.Api.Features.WorldData.Models;

namespace Lorrgs.Api.Features.WorldData.GetRoleSpecs;

/// <summary>Get all specs for a specific role response.</summary>
public sealed record GetRoleSpecsResponse(List<WowSpec> Specs);
