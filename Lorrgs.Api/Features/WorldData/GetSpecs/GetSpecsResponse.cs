using Lorrgs.Api.Features.WorldData.Models;

namespace Lorrgs.Api.Features.WorldData.GetSpecs;

/// <summary>Get all WoW specs, optionally filtered by role or class response.</summary>
public sealed record GetSpecsResponse(List<WowSpec> Specs);
