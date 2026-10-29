namespace Lorrgs.Api.Features.WorldData.GetZone;

public static class GetZoneEndpoint
{
    public static RouteHandlerBuilder MapGetZone(this RouteGroupBuilder group) =>
        group.MapGet("zones/{zoneSlug}", GetZoneHandler.Handle)
            .WithName("GetZone")
            .WithSummary("Get a specific zone by slug");
}
