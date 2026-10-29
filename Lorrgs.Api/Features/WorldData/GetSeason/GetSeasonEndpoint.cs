namespace Lorrgs.Api.Features.WorldData.GetSeason;

public static class GetSeasonEndpoint
{
    public static RouteHandlerBuilder MapGetSeason(this RouteGroupBuilder group) =>
        group.MapGet("seasons/{seasonSlug}", GetSeasonHandler.Handle)
            .WithName("GetSeason")
            .WithSummary("Get a specific season by slug");
}
