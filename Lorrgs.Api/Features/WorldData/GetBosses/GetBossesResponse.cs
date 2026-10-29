using Lorrgs.Api.Features.WorldData.Models;

namespace Lorrgs.Api.Features.WorldData.GetBosses;

/// <summary>Get all raid bosses response.</summary>
public sealed record GetBossesResponse(List<RaidBoss> Bosses);
