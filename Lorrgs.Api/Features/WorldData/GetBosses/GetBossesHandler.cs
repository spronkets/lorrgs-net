using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetBosses;

public static class GetBossesHandler
{
    public static Ok<GetBossesResponse> Handle(
        WorldDataService data, ILoggerFactory loggerFactory,
        [FromQuery] string? edition, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GetBosses");
        var bosses = data.Bosses.OrderBy(b => b.Name).ToList();
        logger.LogInformation("Found {BossCount} bosses", bosses.Count);

        return TypedResults.Ok(new GetBossesResponse(bosses));
    }
}
