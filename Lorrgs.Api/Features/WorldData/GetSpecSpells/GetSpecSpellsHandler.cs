using Lorrgs.Api.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Lorrgs.Api.Features.WorldData.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetSpecSpells;

public static class GetSpecSpellsHandler
{
    public static Results<Ok<GetSpecSpellsResponse>, NotFound<ApiErrorResponse>> Handle(
        WorldDataService data,
        [AsParameters] GetSpecSpellsRequest request, CancellationToken ct)
    {
        var spec = data.GetSpec(request.SpecSlug);
        if (spec == null)
        {
            return TypedResults.NotFound(new ApiErrorResponse($"Spec not found: {request.SpecSlug}"));
        }

        // Spell data per spec is populated from WCL data and stored externally
        return TypedResults.Ok(new GetSpecSpellsResponse(Array.Empty<WowSpell>()));
    }
}
