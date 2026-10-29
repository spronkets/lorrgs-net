using Lorrgs.Api.Features.WorldData.Models;

namespace Lorrgs.Api.Features.WorldData.GetSpells;

/// <summary>Get all WoW spells response.</summary>
public sealed record GetSpellsResponse(List<WowSpell> Spells);
