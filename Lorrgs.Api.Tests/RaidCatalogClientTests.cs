using Lorrgs.Api.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Lorrgs.Api.Tests;

public sealed class RaidCatalogClientTests
{
    [Fact]
    public async Task GetCatalogOrdersEditionsMapsNormalizedBossNamesAndPreservesConfiguration()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var options = new RaidCatalogOptions
        {
            Editions =
            [
                new() { Slug = "retail", Name = "Retail", Order = 2, Public = false },
                new()
                {
                    Slug = "anniversary", Name = "Anniversary", Order = 1, Public = true,
                    Raids = [new()
                    {
                        Slug = "zulaman", Name = "Zul'Aman", ZoneId = 1, Phase = 2, Public = false,
                        Bosses = ["ZULPHIX", "Unknown Boss", " "]
                    }]
                }
            ]
        };
        var client = new RaidCatalogClient(NullLogger<RaidCatalogClient>.Instance, cache, Options.Create(options));

        var catalog = await client.GetCatalogAsync();

        Assert.Equal(["anniversary", "retail"], catalog.Editions.Select(edition => edition.Slug));
        Assert.False(catalog.Editions[1].Public);
        Assert.Empty(catalog.Instances["RETAIL"]);
        var raid = Assert.Single(catalog.Instances["ANNIVERSARY"]);
        Assert.Equal("anniversary", raid.Edition);
        Assert.Equal("zulaman", raid.Slug);
        Assert.Equal("Zul'Aman", raid.Name);
        Assert.Equal(1, raid.ZoneId);
        Assert.Equal(2, raid.Phase);
        Assert.False(raid.Public);
        Assert.Equal(2, raid.Bosses.Count);
        Assert.True(raid.Bosses[0].Mapped);
        Assert.True(raid.Bosses[0].Id > 0);
        Assert.Equal("zul-phix", raid.Bosses[0].Slug);
        Assert.Equal("ZULPHIX", raid.Bosses[0].Name);
        Assert.False(raid.Bosses[1].Mapped);
        Assert.Equal(0, raid.Bosses[1].Id);
        Assert.Equal("", raid.Bosses[1].Slug);
        Assert.Same(catalog, await client.GetCatalogAsync());
    }

    [Fact]
    public async Task EmptyConfigurationProducesEmptyCatalog()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new RaidCatalogClient(NullLogger<RaidCatalogClient>.Instance, cache,
            Options.Create(new RaidCatalogOptions()));
        var catalog = await client.GetCatalogAsync();
        Assert.Empty(catalog.Editions);
        Assert.Empty(catalog.Instances);
    }
}
