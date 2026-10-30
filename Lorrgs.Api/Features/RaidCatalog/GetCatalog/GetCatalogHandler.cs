using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.RaidCatalog.GetCatalog;

public sealed class GetCatalogHandler(ILogger<GetCatalogHandler> logger, RaidCatalogClient raidCatalogClient)
{
    public async Task<Ok<RaidCatalog>> Handle(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RaidCatalog catalog = await raidCatalogClient.GetCatalogAsync(cancellationToken);
        logger.LogInformation("Found raid catalog with {EditionCount} editions and {InstanceCount} instances",
            catalog.Editions.Count, catalog.Instances.Values.Sum(instances => instances.Count));
        return TypedResults.Ok(catalog);
    }
}
