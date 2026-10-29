using Lorrgs.Api.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Lorrgs.Api.Features.WorldData.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetZoneBosses;

public static class GetZoneBossesHandler
{
    public static Results<Ok<GetZoneBossesResponse>, NotFound<ApiErrorResponse>> Handle(
        WorldDataService data, ILoggerFactory loggerFactory,
        [AsParameters] GetZoneBossesRequest request, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GetZoneBosses");
        logger.LogInformation("Fetching bosses for zone: {ZoneSlug}", request.ZoneSlug);
        var zone = data.GetZone(request.ZoneSlug);
        if (zone == null)
        {
            return TypedResults.NotFound(new ApiErrorResponse($"Zone not found: {request.ZoneSlug}"));
        }

        var bosses = data.Bosses
            .Where(b => b.RaidId == zone.Id)
            .ToList();

        return TypedResults.Ok(new GetZoneBossesResponse(bosses));
    }
}
