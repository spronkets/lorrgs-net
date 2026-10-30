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
    public async Task AnniversarySunwellIsUnavailable()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
            .Build();
        var options = new RaidCatalogOptions();
        configuration.Bind(options);
        var client = new RaidCatalogClient(NullLogger<RaidCatalogClient>.Instance, Options.Create(options));
        var catalog = await client.GetCatalogAsync();
        var sunwell = Assert.Single(catalog.Instances["anniversary"], raid => raid.Slug == "sunwellplateau");
        Assert.False(sunwell.IsAvailable);
        Assert.Equal(4, Assert.Single(catalog.Editions, edition => edition.Slug == "anniversary").ActivePhase);
        Assert.All(catalog.Instances["anniversary"].Where(raid => raid.Phase <= 4),
            raid => Assert.True(raid.IsAvailable));
        Assert.All(catalog.Instances.Where(entry => entry.Key != "anniversary").SelectMany(entry => entry.Value),
            raid => Assert.True(raid.IsAvailable));
    }

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
                new() { Slug = "retail", Name = "Retail", Order = 2 },
                new()
                {
                    Slug = "anniversary", Name = "Anniversary", Order = 1, ActivePhase = 1,
                    Raids = [new()
                    {
                        Slug = "zulaman", Name = "Zul'Aman", ZoneId = 1, Phase = 2,
                        Bosses = ["ZULPHIX", "Unknown Boss", " "]
                    }]
                }
            ]
        };
        var client = new RaidCatalogClient(NullLogger<RaidCatalogClient>.Instance, Options.Create(options));

        var catalog = await client.GetCatalogAsync();

        Assert.Equal(["anniversary", "retail"], catalog.Editions.Select(edition => edition.Slug));
        Assert.Empty(catalog.Instances["RETAIL"]);
        var raid = Assert.Single(catalog.Instances["ANNIVERSARY"]);
        Assert.Equal("anniversary", raid.Edition);
        Assert.Equal("zulaman", raid.Slug);
        Assert.Equal("Zul'Aman", raid.Name);
        Assert.Equal(1, raid.ZoneId);
        Assert.Equal(2, raid.Phase);
        Assert.False(raid.IsAvailable);
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

    [Theory]
    [InlineData(0, false, false, false)]
    [InlineData(3, true, false, false)]
    [InlineData(4, true, true, false)]
    [InlineData(5, true, true, true)]
    public async Task ActivePhaseDeterminesRaidAvailability(int activePhase, bool earlier, bool current, bool future)
    {
        var client = new RaidCatalogClient(NullLogger<RaidCatalogClient>.Instance,
            Options.Create(new RaidCatalogOptions
            {
                Editions = [new()
                {
                    Slug = "anniversary", ActivePhase = activePhase,
                    Raids = [new() { Phase = 3 }, new() { Phase = 4 }, new() { Phase = 5 }]
                }]
            }));

        var catalog = await client.GetCatalogAsync();

        Assert.Equal(activePhase, Assert.Single(catalog.Editions).ActivePhase);
        Assert.Equal([earlier, current, future],
            catalog.Instances["anniversary"].Select(raid => raid.IsAvailable));
    }
}
