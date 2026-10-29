using Lorrgs.Api.Controllers;
using Lorrgs.Api.Models;
using Lorrgs.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Lorrgs.Api.Tests;

public sealed class RaidCatalogControllerTests
{
    [Fact]
    public async Task GetCatalogReturnsTheConfiguredCatalogFromTheService()
    {
        var client = new RaidCatalogClient(NullLogger<RaidCatalogClient>.Instance,
            Options.Create(new RaidCatalogOptions
            {
                Editions = [new() { Slug = "retail", Name = "Retail", Public = true }]
            }));
        var controller = new RaidCatalogController(NullLogger<RaidCatalogController>.Instance, client);

        var response = Assert.IsType<OkObjectResult>(await controller.GetCatalog());

        var catalog = Assert.IsType<RaidCatalog>(response.Value);
        Assert.Equal("retail", Assert.Single(catalog.Editions).Slug);
        Assert.Empty(catalog.Instances["retail"]);
        Assert.NotSame(await client.GetCatalogAsync(), catalog);
    }
}
