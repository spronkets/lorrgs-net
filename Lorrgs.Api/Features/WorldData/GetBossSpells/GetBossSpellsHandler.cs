using Lorrgs.Api.Shared.Responses;
using Lorrgs.Api.Features.WorldData.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetBossSpells;

public static class GetBossSpellsHandler
{
    public static Results<Ok<GetBossSpellsResponse>, NotFound<ApiErrorResponse>> Handle(
        WorldDataService data, ILoggerFactory loggerFactory,
        [AsParameters] GetBossSpellsRequest request, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GetBossSpells");
        var boss = data.GetBoss(request.BossSlug);
        if (boss == null)
        {
            logger.LogWarning("Boss not found");
            return TypedResults.NotFound(new ApiErrorResponse($"Boss not found: {request.BossSlug}"));
        }

        logger.LogInformation("Found {SpellCount} spells for boss: {BossId} {BossName}",
            0, boss.Id, boss.Name);
        return TypedResults.Ok(new GetBossSpellsResponse(Array.Empty<WowSpell>()));
    }
}
