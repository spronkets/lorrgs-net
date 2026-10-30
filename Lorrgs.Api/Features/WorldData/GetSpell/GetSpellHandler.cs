using Lorrgs.Api.Shared.Responses;
using Lorrgs.Api.Features.WorldData.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetSpell;

public static class GetSpellHandler
{
    public static Results<Ok<WowSpell>, NotFound<ApiErrorResponse>> Handle(
        WorldDataService data, ILoggerFactory loggerFactory,
        [AsParameters] GetSpellRequest request, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GetSpell");
        var spell = data.GetSpell(request.SpellId);
        if (spell == null)
        {
            logger.LogWarning("Spell not found");
            return TypedResults.NotFound(new ApiErrorResponse($"Spell not found: {request.SpellId}"));
        }

        logger.LogInformation("Found spell: {SpellId} {SpellName}", spell.Id, spell.Name);
        return TypedResults.Ok(spell);
    }
}
