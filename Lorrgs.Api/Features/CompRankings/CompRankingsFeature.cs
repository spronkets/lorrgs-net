using Lorrgs.Api.Features.CompRankings.GetCompRanking;
using Lorrgs.Api.Features.CompRankings.GetDetails;
using Lorrgs.WarcraftLogs.Services;

namespace Lorrgs.Api.Features.CompRankings;

public static class CompRankingsFeature
{
    public static IServiceCollection AddCompRankings(this IServiceCollection services)
    {
        services.AddScoped<ICompRankingService, CompRankingService>();
        services.AddScoped<GetCompRankingHandler>();
        services.AddScoped<GetDetailsHandler>();
        return services;
    }

    public static RouteGroupBuilder MapCompRankings(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/rankings/comp").WithTags("CompRankings");
        group.MapGetCompRanking();
        group.MapGetDetails();
        return group;
    }
}
