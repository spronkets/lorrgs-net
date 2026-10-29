using Lorrgs.Api.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
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
        logger.LogInformation("Fetching zone: {ZoneSlug}", request.ZoneSlug);
        var zone = data.GetZone(request.ZoneSlug);
        if (zone == null)
        {
            return TypedResults.NotFound(new ApiErrorResponse($"Zone not found: {request.ZoneSlug}"));
        }

        return TypedResults.Ok(zone);
    }
}
