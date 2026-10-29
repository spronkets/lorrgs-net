using Lorrgs.Api.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Lorrgs.Api.Features.WorldData.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetBossSpells;

public static class GetBossSpellsHandler
{
    public static Results<Ok<GetBossSpellsResponse>, NotFound<ApiErrorResponse>> Handle(
        WorldDataService data,
        [AsParameters] GetBossSpellsRequest request, CancellationToken ct)
    {
        var boss = data.GetBoss(request.BossSlug);
        if (boss == null)
        {
            return TypedResults.NotFound(new ApiErrorResponse($"Boss not found: {request.BossSlug}"));
        }

        return TypedResults.Ok(new GetBossSpellsResponse(Array.Empty<WowSpell>()));
    }
}
