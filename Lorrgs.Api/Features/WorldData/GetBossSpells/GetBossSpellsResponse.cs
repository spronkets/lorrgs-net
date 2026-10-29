using Lorrgs.Api.Features.WorldData.Models;

namespace Lorrgs.Api.Features.WorldData.GetBossSpells;

/// <summary>Get all spells for a boss (boss abilities tracked in timeline) response.</summary>
public sealed record GetBossSpellsResponse(WowSpell[] Spells);
