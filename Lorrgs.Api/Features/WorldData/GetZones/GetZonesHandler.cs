using Microsoft.AspNetCore.Mvc;
using Lorrgs.Api.Features.WorldData.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetZones;

public static class GetZonesHandler
{
    public static Ok<GetZonesResponse> Handle(
        WorldDataService data, ILoggerFactory loggerFactory,
        [FromQuery] string? edition, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GetZones");
        logger.LogInformation("Fetching all zones");
        var zones = data.Zones.OrderByDescending(z => z.Id).ToList();

        return TypedResults.Ok(new GetZonesResponse(zones));
    }
}
