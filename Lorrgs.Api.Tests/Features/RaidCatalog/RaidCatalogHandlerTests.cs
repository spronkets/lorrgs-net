using Lorrgs.Api.Features.RaidCatalog;
using Lorrgs.Api.Features.RaidCatalog.GetCatalog;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Lorrgs.Api.Tests;

public sealed class RaidCatalogHandlerTests
{
    [Fact]
    public async Task GetCatalogReturnsTheConfiguredCatalogFromTheService()
    {
        var client = new RaidCatalogClient(NullLogger<RaidCatalogClient>.Instance,
            Options.Create(new RaidCatalogOptions
            {
                Editions = [new() { Slug = "retail", Name = "Retail", Public = true }]
            }));
        var handlers = new GetCatalogHandler(NullLogger<GetCatalogHandler>.Instance, client);

        var response = HttpResultAssertions.Status(200, await handlers.Handle());

        var catalog = Assert.IsType<RaidCatalog>(response.Value);
        Assert.Equal("retail", Assert.Single(catalog.Editions).Slug);
        Assert.Empty(catalog.Instances["retail"]);
        Assert.NotSame(await client.GetCatalogAsync(), catalog);
    }
}
