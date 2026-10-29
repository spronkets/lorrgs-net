namespace Lorrgs.Api.Features.WarcraftLogs.GetRankings;

public static class GetRankingsEndpoint
{
    public static RouteHandlerBuilder MapGetRankings(this RouteGroupBuilder group) =>
        group.MapGet("rankings", ([AsParameters] GetRankingsRequest request, GetRankingsHandler handler, CancellationToken cancellationToken) =>
            handler.Handle(request, cancellationToken))
            .WithName("GetRankings")
            .WithSummary("Get Rankings")
            .Produces<GetRankingsResponse[]>();
}
