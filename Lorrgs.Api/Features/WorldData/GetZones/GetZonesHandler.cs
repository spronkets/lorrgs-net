using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetZones;

public static class GetZonesHandler
{
    public static Ok<GetZonesResponse> Handle(
        WorldDataService data, ILoggerFactory loggerFactory,
        [FromQuery] string? edition, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GetZones");
        var zones = data.Zones.OrderByDescending(z => z.Id).ToList();
        logger.LogInformation("Found {ZoneCount} zones", zones.Count);

        return TypedResults.Ok(new GetZonesResponse(zones));
    }
}
