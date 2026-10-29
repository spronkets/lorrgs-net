using Lorrgs.Api.Features.Rankings.GetSpecRanking;
using Lorrgs.Api.Features.Rankings.GetSpecRankingInfo;
using Lorrgs.Api.Features.Rankings.MarkSpecRankingDirty;
using Lorrgs.Api.Features.Rankings.QueueCompRankingUpdate;
using Lorrgs.Api.Features.Rankings.QueueSpecRankingUpdate;
using Lorrgs.WarcraftLogs.Rankings;

namespace Lorrgs.Api.Features.Rankings;

public static class RankingsFeature
{
    public static IServiceCollection AddRankings(this IServiceCollection services)
    {
        services.AddScoped<RankingsDataService>();
        services.AddScoped<GetSpecRankingHandler>();
        services.AddScoped<GetSpecRankingInfoHandler>();
        services.AddScoped<QueueSpecRankingUpdateHandler>();
        services.AddScoped<QueueCompRankingUpdateHandler>();
        services.AddScoped<MarkSpecRankingDirtyHandler>();
        return services;
    }

    public static RouteGroupBuilder MapRankings(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/Rankings").WithTags("Rankings");
        group.MapGetSpecRanking();
        group.MapGetSpecRankingInfo();
        group.MapQueueSpecRankingUpdate();
        group.MapQueueCompRankingUpdate();
        group.MapMarkSpecRankingDirty();
        return group;
    }
}
