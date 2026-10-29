namespace Lorrgs.Api.Features.WorldData.GetTrinket;

public static class GetTrinketEndpoint
{
    public static RouteHandlerBuilder MapGetTrinket(this RouteGroupBuilder group) =>
        group.MapGet("trinkets/{trinketId}", GetTrinketHandler.Handle)
            .WithName("GetTrinket")
            .WithSummary("Get a specific trinket by ID");
}
