using Lorrgs.Api.Shared.Responses;
using Lorrgs.WarcraftLogs.Models;

namespace Lorrgs.Api.Features.Rankings.GetSpecRankingInfo;

public static class GetSpecRankingInfoEndpoint
{
    public static RouteHandlerBuilder MapGetSpecRankingInfo(this RouteGroupBuilder group) =>
        group.MapGet("spec/{specSlug}/{bossSlug}/info", ([AsParameters] GetSpecRankingInfoRequest request, GetSpecRankingInfoHandler handler, CancellationToken cancellationToken) =>
            handler.Handle(request, cancellationToken))
            .WithName("GetSpecRankingInfo")
            .WithSummary("Get ranking info without the full report details. Lighter response for quick info (percentile, updated time, etc).")
            .Produces<SpecRankingData>()
            .Produces<ApiErrorResponse>(400)
            .Produces<ApiErrorResponse>(404)
            .Produces<ApiErrorResponse>(429)
            .Produces<ApiErrorResponse>(500);
}
