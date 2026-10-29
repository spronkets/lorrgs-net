using Lorrgs.Api.Features.WorldData.Models;

namespace Lorrgs.Api.Features.WorldData.GetRoles;

/// <summary>Get all WoW roles (Tank, Healer, Melee DPS, Ranged DPS) response.</summary>
public sealed record GetRolesResponse(List<WowRole> Roles);
