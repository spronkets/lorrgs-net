using Lorrgs.WarcraftLogs.Models;

namespace Lorrgs.Api.Features.CompRankings.GetCompRanking;

public static class GetCompRankingEndpoint
{
    public static RouteHandlerBuilder MapGetCompRanking(this RouteGroupBuilder group) =>
        group.MapGet("{bossSlug}", ([AsParameters] GetCompRankingRequest request, GetCompRankingHandler handler, CancellationToken cancellationToken) =>
            handler.Handle(request, cancellationToken))
            .WithName("GetCompRanking")
            .WithSummary("Return the complete execution-ranked summary page. Composition is uninspected until details are requested.")
            .Produces<CompRankingData>()
            .ProducesProblem(400)
            .ProducesProblem(404)
            .ProducesProblem(429)
            .ProducesProblem(502);
}
