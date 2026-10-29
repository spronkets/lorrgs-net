using Lorrgs.Api.Features.WorldData.Models;

namespace Lorrgs.Api.Features.WorldData.GetClassSpecs;

/// <summary>Get all specs for a specific class response.</summary>
public sealed record GetClassSpecsResponse(List<WowSpec> Specs);
