using Microsoft.AspNetCore.Mvc;
using Lorrgs.Api.Features.WorldData.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetTrinkets;

public static class GetTrinketsHandler
{
    public static Ok<GetTrinketsResponse> Handle(
        WorldDataService data, ILoggerFactory loggerFactory,
        [FromQuery] string? edition, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GetTrinkets");
        logger.LogInformation("Fetching all trinkets");
        var trinkets = data.Trinkets.OrderBy(t => t.Name).ToList();

        return TypedResults.Ok(new GetTrinketsResponse(trinkets));
    }
}
