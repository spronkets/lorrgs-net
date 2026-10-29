using Lorrgs.Api.Features.WorldData.Models;

namespace Lorrgs.Api.Features.WorldData.GetTrinkets;

/// <summary>Get all WoW trinkets response.</summary>
public sealed record GetTrinketsResponse(List<WowTrinket> Trinkets);
