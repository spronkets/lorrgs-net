using Lorrgs.Api.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
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
        logger.LogInformation("Fetching spell: {SpellId}", request.SpellId);
        var spell = data.GetSpell(request.SpellId);
        if (spell == null)
        {
            return TypedResults.NotFound(new ApiErrorResponse($"Spell not found: {request.SpellId}"));
        }

        return TypedResults.Ok(spell);
    }
}
