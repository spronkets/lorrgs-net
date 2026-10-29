namespace Lorrgs.Api.Features.WorldData.GetTrinkets;

public static class GetTrinketsEndpoint
{
    public static RouteHandlerBuilder MapGetTrinkets(this RouteGroupBuilder group) =>
        group.MapGet("trinkets", GetTrinketsHandler.Handle)
            .WithName("GetTrinkets")
            .WithSummary("Get all WoW trinkets");
}
