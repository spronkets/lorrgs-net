using Lorrgs.Api.Features.CompRankings;
using Lorrgs.Api.Features.RaidCatalog;
using Lorrgs.Api.Features.Rankings;
using Lorrgs.Api.Features.ReportTimeline;
using Lorrgs.Api.Features.WarcraftLogs;
using Lorrgs.Api.Features.WorldData;
using Lorrgs.WarcraftLogs.Services;

namespace Lorrgs.Api;

public static class ApiConfiguration
{
    public static IServiceCollection AddApiFeatures(this IServiceCollection services)
    {
        services.AddWorldData();
        services.AddRankings();
        services.AddCompRankings();
        services.AddReportTimeline();
        services.AddWarcraftLogs();
        services.AddRaidCatalog();
        services.AddHttpClient<Lorrgs.WarcraftLogs.WarcraftLogsClient>();
        services.AddScoped<IReportTimelineService, ReportTimelineService>();
        return services;
    }

    public static RouteGroupBuilder MapApi(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api");
        api.MapWorldData();
        api.MapRankings();
        api.MapCompRankings();
        api.MapReportTimeline();
        api.MapWarcraftLogs();
        api.MapRaidCatalog();
        return api;
    }
}
