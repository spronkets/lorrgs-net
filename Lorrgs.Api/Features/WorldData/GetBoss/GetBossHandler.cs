using Lorrgs.Api.Shared.Responses;
using Lorrgs.Api.Features.WorldData.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetBoss;

public static class GetBossHandler
{
    public static Results<Ok<RaidBoss>, NotFound<ApiErrorResponse>> Handle(
        WorldDataService data, ILoggerFactory loggerFactory,
        [AsParameters] GetBossRequest request, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GetBoss");
        var boss = data.Bosses.FirstOrDefault(b => b.NameSlug == request.BossSlug);
        if (boss == null)
        {
            logger.LogWarning("Boss not found");
            return TypedResults.NotFound(new ApiErrorResponse($"Boss not found: {request.BossSlug}"));
        }

        logger.LogInformation("Found boss: {BossId} {BossName}", boss.Id, boss.Name);
        return TypedResults.Ok(boss);
    }
}
