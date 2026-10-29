namespace Lorrgs.Api.Features.WorldData.GetZoneBosses;

public static class GetZoneBossesEndpoint
{
    public static RouteHandlerBuilder MapGetZoneBosses(this RouteGroupBuilder group) =>
        group.MapGet("zones/{zoneSlug}/bosses", GetZoneBossesHandler.Handle)
            .WithName("GetZoneBosses")
            .WithSummary("Get all bosses in a zone");
}
