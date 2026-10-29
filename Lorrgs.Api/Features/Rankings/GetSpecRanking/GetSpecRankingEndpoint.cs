using Lorrgs.Api.Shared.Responses;
using Lorrgs.WarcraftLogs.Models;

namespace Lorrgs.Api.Features.Rankings.GetSpecRanking;

public static class GetSpecRankingEndpoint
{
    public static RouteHandlerBuilder MapGetSpecRanking(this RouteGroupBuilder group) =>
        group.MapGet("spec/{specSlug}/{bossSlug}", ([AsParameters] GetSpecRankingRequest request, GetSpecRankingHandler handler, CancellationToken cancellationToken) =>
            handler.Handle(request, cancellationToken))
            .WithName("GetSpecRanking")
            .WithSummary("Get full ranking data for a spec on a specific boss. Includes all top reports with fight details, player information and complete API-cached timelines.")
            .Produces<SpecRankingData>()
            .Produces<ApiErrorResponse>(400)
            .Produces<ApiErrorResponse>(404)
            .Produces<ApiErrorResponse>(429)
            .Produces<ApiErrorResponse>(500);
}
