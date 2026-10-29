namespace Lorrgs.Api.Features.WorldData.GetSeasons;

public static class GetSeasonsEndpoint
{
    public static RouteHandlerBuilder MapGetSeasons(this RouteGroupBuilder group) =>
        group.MapGet("seasons", GetSeasonsHandler.Handle)
            .WithName("GetSeasons")
            .WithSummary("Get all seasons");
}
