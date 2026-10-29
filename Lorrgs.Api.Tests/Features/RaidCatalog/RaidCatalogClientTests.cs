using Lorrgs.Api.Features.RaidCatalog;
using Lorrgs.Api.Features.RaidCatalog.GetCatalog;
using Lorrgs.Api.Features.WorldData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Lorrgs.Api.Tests;

public sealed class RaidCatalogClientTests
{
    [Fact]
    public async Task AnniversaryZulAmanUsesClassicFreshZoneAndOriginalFinalBoss()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
            .Build();
        var options = new RaidCatalogOptions();
        configuration.Bind(options);
        var client = new RaidCatalogClient(NullLogger<RaidCatalogClient>.Instance, Options.Create(options));

        var catalog = await client.GetCatalogAsync();
        var raid = Assert.Single(catalog.Instances["anniversary"], raid => raid.Slug == "zulaman");

        Assert.Equal(1062, raid.ZoneId);
        Assert.Equal(
            ["Nalorakk", "Akil'zon", "Jan'alai", "Halazzi", "Hex Lord Malacrass", "Zul'jin"],
            raid.Bosses.Select(boss => boss.Name));
        Assert.Equal([101190, 101189, 101191, 101192, 101193, 101194],
            raid.Bosses.Select(boss => boss.Id));
        Assert.Equal(["nalorakk", "akilzon", "janalai", "halazzi", "hex-lord-malacrass", "zuljin"],
            raid.Bosses.Select(boss => boss.Slug));
        Assert.All(raid.Bosses, boss => Assert.True(boss.Mapped));
        foreach (var slug in new[] { "akilzon", "nalorakk", "janalai", "halazzi", "hex-lord-malacrass" })
        {
            var boss = Assert.Single(WorldDataService.Instance.Bosses, boss => boss.NameSlug == slug);
            Assert.Equal($"ui-ej-boss-{slug}.png", boss.Icon);
        }
        Assert.Equal("achievement_boss_zuljin.jpg",
            Assert.Single(WorldDataService.Instance.Bosses, boss => boss.NameSlug == "zuljin").Icon);
    }

    [Fact]
    public async Task GetCatalogOrdersEditionsMapsNormalizedBossNamesAndPreservesConfiguration()
    {
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
        var client = new RaidCatalogClient(NullLogger<RaidCatalogClient>.Instance, Options.Create(options));

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
        var nextCatalog = await client.GetCatalogAsync();
        Assert.NotSame(catalog, nextCatalog);
        Assert.Equal(catalog.Editions.Select(edition => edition.Slug),
            nextCatalog.Editions.Select(edition => edition.Slug));
    }

    [Fact]
    public async Task EmptyConfigurationProducesEmptyCatalog()
    {
        var client = new RaidCatalogClient(NullLogger<RaidCatalogClient>.Instance,
            Options.Create(new RaidCatalogOptions()));
        var catalog = await client.GetCatalogAsync();
        Assert.Empty(catalog.Editions);
        Assert.Empty(catalog.Instances);
    }
}
