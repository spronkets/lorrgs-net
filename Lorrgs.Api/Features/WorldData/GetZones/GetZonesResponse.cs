using Lorrgs.Api.Features.WorldData.Models;

namespace Lorrgs.Api.Features.WorldData.GetZones;

/// <summary>Get all raid zones response.</summary>
public sealed record GetZonesResponse(List<RaidZone> Zones);
