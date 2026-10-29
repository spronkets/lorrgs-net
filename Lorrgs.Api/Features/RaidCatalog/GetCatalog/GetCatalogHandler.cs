using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.RaidCatalog.GetCatalog;

public sealed class GetCatalogHandler(ILogger<GetCatalogHandler> logger, RaidCatalogClient raidCatalogClient)
{
    public async Task<Ok<RaidCatalog>> Handle(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Fetching raid catalog");

        RaidCatalog catalog = await raidCatalogClient.GetCatalogAsync(cancellationToken);
        return TypedResults.Ok(catalog);
    }
}
