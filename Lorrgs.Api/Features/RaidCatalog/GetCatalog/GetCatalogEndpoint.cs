namespace Lorrgs.Api.Features.RaidCatalog.GetCatalog;

public static class GetCatalogEndpoint
{
    public static RouteHandlerBuilder MapGetCatalog(this RouteGroupBuilder group) =>
        group.MapGet("", (GetCatalogHandler handler, CancellationToken cancellationToken) =>
            handler.Handle(cancellationToken))
            .WithName("GetCatalog")
            .WithSummary("Get Catalog")
            .Produces<RaidCatalog>();
}
