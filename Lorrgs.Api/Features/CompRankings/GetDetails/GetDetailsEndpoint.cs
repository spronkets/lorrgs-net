using Lorrgs.WarcraftLogs.Models;

namespace Lorrgs.Api.Features.CompRankings.GetDetails;

public static class GetDetailsEndpoint
{
    public static RouteHandlerBuilder MapGetDetails(this RouteGroupBuilder group) =>
        group.MapGet("reports/{code}/fights/{fightId:int}", ([AsParameters] GetDetailsRequest request, GetDetailsHandler handler, CancellationToken cancellationToken) =>
            handler.Handle(request, cancellationToken))
            .WithName("GetDetails")
            .WithSummary("Inspect one kill's composition and timelines using shared metadata and cooldown events from every fight in its report.")
            .Produces<RankingReport>()
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(429)
            .ProducesProblem(502);
}
