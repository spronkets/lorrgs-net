using Lorrgs.Api.Shared.Responses;
using Lorrgs.Api.Features.WorldData.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetSpecSpells;

public static class GetSpecSpellsHandler
{
    public static Results<Ok<GetSpecSpellsResponse>, NotFound<ApiErrorResponse>> Handle(
        WorldDataService data, ILoggerFactory loggerFactory,
        [AsParameters] GetSpecSpellsRequest request, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GetSpecSpells");
        var spec = data.GetSpec(request.SpecSlug);
        if (spec == null)
        {
            logger.LogWarning("Spec not found");
            return TypedResults.NotFound(new ApiErrorResponse($"Spec not found: {request.SpecSlug}"));
        }

        // Spell data per spec is populated from WCL data and stored externally
        logger.LogInformation("Found {SpellCount} spells for spec: {SpecId} {SpecName}",
            0, spec.Id, spec.FullName);
        return TypedResults.Ok(new GetSpecSpellsResponse(Array.Empty<WowSpell>()));
    }
}
