using Lorrgs.Api.Features.ReportTimeline.GetBossTimeline;
using Lorrgs.Api.Features.ReportTimeline.GetPlayerTimeline;
using Lorrgs.WarcraftLogs.Models;

namespace Lorrgs.Api.Features.ReportTimeline;

public static class ReportTimelineFeature
{
    public static IServiceCollection AddReportTimeline(this IServiceCollection services)
    {
        services.AddScoped<TimelineLoader>();
        services.AddScoped<GetPlayerTimelineHandler>();
        services.AddScoped<GetBossTimelineHandler>();
        return services;
    }

    public static RouteGroupBuilder MapReportTimeline(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/warcraftlogs/reports/{reportCode}/timeline").WithTags("ReportTimeline");
        group.MapGetPlayerTimeline();
        group.MapGetBossTimeline();
        return group;
    }
}
