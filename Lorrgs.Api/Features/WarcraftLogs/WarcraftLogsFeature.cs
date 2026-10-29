using Lorrgs.Api.Features.WarcraftLogs.AnalyzeRotation;
using Lorrgs.Api.Features.WarcraftLogs.GetRankings;
using Lorrgs.Api.Features.WarcraftLogs.GetReport;
using Lorrgs.Api.Features.WarcraftLogs.GetSeasons;
using Lorrgs.Api.Features.WarcraftLogs.LookupReport;
using Lorrgs.WarcraftLogs.Services;

namespace Lorrgs.Api.Features.WarcraftLogs;

public static class WarcraftLogsFeature
{
    public static IServiceCollection AddWarcraftLogs(this IServiceCollection services)
    {
        services.AddScoped<RotationAnalysisService>();
        services.AddScoped<ReportLookupService>();
        services.AddScoped<GetSeasonsHandler>();
        services.AddScoped<GetRankingsHandler>();
        services.AddScoped<GetReportHandler>();
        services.AddScoped<LookupReportHandler>();
        services.AddScoped<AnalyzeRotationHandler>();
        return services;
    }

    public static RouteGroupBuilder MapWarcraftLogs(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/WarcraftLogs").WithTags("WarcraftLogs");
        group.MapGetSeasons();
        group.MapGetRankings();
        group.MapGetReport();
        group.MapLookupReport();
        group.MapAnalyzeRotation();
        return group;
    }
}
