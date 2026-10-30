using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetTrinkets;

public static class GetTrinketsHandler
{
    public static Ok<GetTrinketsResponse> Handle(
        WorldDataService data, ILoggerFactory loggerFactory,
        [FromQuery] string? edition, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GetTrinkets");
        var trinkets = data.Trinkets.OrderBy(t => t.Name).ToList();
        logger.LogInformation("Found {TrinketCount} trinkets", trinkets.Count);

        return TypedResults.Ok(new GetTrinketsResponse(trinkets));
    }
}
