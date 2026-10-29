namespace Lorrgs.Api.Features.WorldData.GetZones;

public static class GetZonesEndpoint
{
    public static RouteHandlerBuilder MapGetZones(this RouteGroupBuilder group) =>
        group.MapGet("zones", GetZonesHandler.Handle)
            .WithName("GetZones")
            .WithSummary("Get all raid zones");
}
