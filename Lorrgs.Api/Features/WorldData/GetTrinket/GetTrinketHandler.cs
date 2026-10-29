using Lorrgs.Api.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Lorrgs.Api.Features.WorldData.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetTrinket;

public static class GetTrinketHandler
{
    public static Results<Ok<WowTrinket>, NotFound<ApiErrorResponse>> Handle(
        WorldDataService data, ILoggerFactory loggerFactory,
        [AsParameters] GetTrinketRequest request, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GetTrinket");
        logger.LogInformation("Fetching trinket: {TrinketId}", request.TrinketId);
        var trinket = data.GetTrinket(request.TrinketId);
        if (trinket == null)
        {
            return TypedResults.NotFound(new ApiErrorResponse($"Trinket not found: {request.TrinketId}"));
        }

        return TypedResults.Ok(trinket);
    }
}
