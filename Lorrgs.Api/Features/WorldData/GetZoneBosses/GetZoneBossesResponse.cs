using Lorrgs.Api.Features.WorldData.Models;

namespace Lorrgs.Api.Features.WorldData.GetZoneBosses;

/// <summary>Get all bosses in a zone response.</summary>
public sealed record GetZoneBossesResponse(List<RaidBoss> Bosses);
