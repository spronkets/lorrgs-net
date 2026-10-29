using Lorrgs.Api.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
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
        logger.LogInformation("Fetching boss: {BossSlug}", request.BossSlug);
        var boss = data.Bosses.FirstOrDefault(b => b.NameSlug == request.BossSlug);
        if (boss == null)
        {
            return TypedResults.NotFound(new ApiErrorResponse($"Boss not found: {request.BossSlug}"));
        }

        return TypedResults.Ok(boss);
    }
}
