using Lorrgs.Api.Features.WorldData.Models;

namespace Lorrgs.Api.Features.WorldData.GetSpecSpells;

/// <summary>Get all spells for a spec (abilities tracked in timeline) response.</summary>
public sealed record GetSpecSpellsResponse(WowSpell[] Spells);
