using Lorrgs.Api.Shared.Responses;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetZoneBosses;

public static class GetZoneBossesHandler
{
    public static Results<Ok<GetZoneBossesResponse>, NotFound<ApiErrorResponse>> Handle(
        WorldDataService data, ILoggerFactory loggerFactory,
        [AsParameters] GetZoneBossesRequest request, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GetZoneBosses");
        var zone = data.GetZone(request.ZoneSlug);
        if (zone == null)
        {
            logger.LogWarning("Zone not found");
            return TypedResults.NotFound(new ApiErrorResponse($"Zone not found: {request.ZoneSlug}"));
        }

        var bosses = data.Bosses
            .Where(b => b.RaidId == zone.Id)
            .ToList();

        logger.LogInformation("Found {BossCount} bosses for zone: {ZoneId} {ZoneName}",
            bosses.Count, zone.Id, zone.Name);
        return TypedResults.Ok(new GetZoneBossesResponse(bosses));
    }
}
