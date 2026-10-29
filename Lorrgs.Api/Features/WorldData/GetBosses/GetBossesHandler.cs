using Microsoft.AspNetCore.Mvc;
using Lorrgs.Api.Features.WorldData.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetBosses;

public static class GetBossesHandler
{
    public static Ok<GetBossesResponse> Handle(
        WorldDataService data, ILoggerFactory loggerFactory,
        [FromQuery] string? edition, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GetBosses");
        logger.LogInformation("Fetching all bosses");
        var bosses = data.Bosses.OrderBy(b => b.Name).ToList();

        return TypedResults.Ok(new GetBossesResponse(bosses));
    }
}
