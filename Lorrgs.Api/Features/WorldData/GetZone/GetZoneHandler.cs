using Lorrgs.Api.Shared.Responses;
using Lorrgs.Api.Features.WorldData.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetZone;

public static class GetZoneHandler
{
    public static Results<Ok<RaidZone>, NotFound<ApiErrorResponse>> Handle(
        WorldDataService data, ILoggerFactory loggerFactory,
        [AsParameters] GetZoneRequest request, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GetZone");
        var zone = data.GetZone(request.ZoneSlug);
        if (zone == null)
        {
            logger.LogWarning("Zone not found");
            return TypedResults.NotFound(new ApiErrorResponse($"Zone not found: {request.ZoneSlug}"));
        }

        logger.LogInformation("Found zone: {ZoneId} {ZoneName}", zone.Id, zone.Name);
        return TypedResults.Ok(zone);
    }
}
