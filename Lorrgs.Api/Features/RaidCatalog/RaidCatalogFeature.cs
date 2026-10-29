using Lorrgs.Api.Features.RaidCatalog.GetCatalog;

namespace Lorrgs.Api.Features.RaidCatalog;

public static class RaidCatalogFeature
{
    public static IServiceCollection AddRaidCatalog(this IServiceCollection services)
    {
        services.AddScoped<RaidCatalogClient>();
        services.AddScoped<GetCatalogHandler>();
        return services;
    }

    public static RouteGroupBuilder MapRaidCatalog(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/raid-catalog").WithTags("RaidCatalog");
        group.MapGetCatalog();
        return group;
    }
}
