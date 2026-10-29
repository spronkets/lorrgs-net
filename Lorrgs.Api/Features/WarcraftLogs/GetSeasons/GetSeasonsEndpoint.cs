namespace Lorrgs.Api.Features.WarcraftLogs.GetSeasons;

public static class GetSeasonsEndpoint
{
    public static RouteHandlerBuilder MapGetSeasons(this RouteGroupBuilder group) =>
        group.MapGet("seasons", (GetSeasonsHandler handler, CancellationToken cancellationToken) =>
            handler.Handle(cancellationToken))
            .WithName("WarcraftLogs.GetSeasons")
            .WithSummary("Get Seasons")
            .Produces<WarcraftLogsSeasonResponse[]>();
}
