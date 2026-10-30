using Lorrgs.Api.Shared.Responses;
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
        var trinket = data.GetTrinket(request.TrinketId);
        if (trinket == null)
        {
            logger.LogWarning("Trinket not found");
            return TypedResults.NotFound(new ApiErrorResponse($"Trinket not found: {request.TrinketId}"));
        }

        logger.LogInformation("Found trinket: {TrinketId} {TrinketName}", trinket.Id, trinket.Name);
        return TypedResults.Ok(trinket);
    }
}
