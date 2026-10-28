using System.Text.Json;
using Lorrgs.Api.Controllers;
using Lorrgs.Api.Models;
using Lorrgs.Api.Services;
using Lorrgs.Api.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lorrgs.Api.Tests;

public sealed class WorldDataControllerTests : IDisposable
{
    private readonly CacheServiceFixture fixture = new();
    private readonly WorldDataService data = WorldDataService.Instance;
    public void Dispose() => fixture.Dispose();

    [Theory]
    [InlineData("roles")]
    [InlineData("classes")]
    [InlineData("bosses")]
    [InlineData("zones")]
    [InlineData("spells")]
    [InlineData("trinkets")]
    [InlineData("seasons")]
    public async Task CollectionEndpointsReturnOrderedCatalogsFromFreshMemoryAndFileCaches(string resource)
    {
        var controller = Controller();
        var expected = resource switch
        {
            "roles" => JsonSerializer.SerializeToElement(new { roles = data.Roles.OrderBy(role => role.Id) }),
            "classes" => JsonSerializer.SerializeToElement(data.Classes.OrderBy(item => item.Name).ToDictionary(item => item.NameSlug)),
            "bosses" => JsonSerializer.SerializeToElement(new { bosses = data.Bosses.OrderBy(item => item.Name) }),
            "zones" => JsonSerializer.SerializeToElement(new { zones = data.Zones.OrderByDescending(item => item.Id) }),
            "spells" => JsonSerializer.SerializeToElement(new { spells = data.Spells.OrderBy(item => item.Name) }),
            "trinkets" => JsonSerializer.SerializeToElement(new { trinkets = data.Trinkets.OrderBy(item => item.Name) }),
            "seasons" => JsonSerializer.SerializeToElement(new { seasons = data.Seasons.OrderBy(item => item.StartDate) }),
            _ => throw new ArgumentOutOfRangeException(nameof(resource))
        };

        var fresh = await Collection(controller, resource);
        var memory = await Collection(controller, resource);
        var file = await Collection(Controller(), resource);

        AssertJson(expected, fresh);
        AssertJson(expected, memory);
        AssertJson(expected, file);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(2, null)]
    [InlineData(null, 2)]
    [InlineData(2, 2)]
    [InlineData(-1, -1)]
    public async Task GetSpecsAppliesCombinedFiltersAndKeepsCacheEntriesSeparate(int? role, int? wowClass)
    {
        var controller = Controller();
        var expected = new
        {
            specs = data.Specs.Where(spec => (!role.HasValue || spec.RoleId == role) &&
                (!wowClass.HasValue || spec.ClassId == wowClass)).OrderBy(spec => spec.FullName)
        };

        AssertJson(expected, await controller.GetSpecs(role, wowClass));
        AssertJson(expected, await controller.GetSpecs(role, wowClass));
        AssertJson(expected, await Controller().GetSpecs(role, wowClass));
        AssertJson(new { specs = data.Specs.OrderBy(spec => spec.FullName) }, await controller.GetSpecs());
    }

    [Theory]
    [InlineData("spec")]
    [InlineData("boss")]
    [InlineData("zone")]
    [InlineData("season")]
    [InlineData("spell")]
    [InlineData("trinket")]
    public async Task ItemEndpointsReturnMatchingCatalogItemAndReuseBothCaches(string resource)
    {
        var controller = Controller();
        object expected = resource switch
        {
            "spec" => data.Specs.First(),
            "boss" => data.Bosses.First(),
            "zone" => data.Zones.First(),
            "season" => data.Seasons.First(),
            "spell" => data.Spells.First(),
            "trinket" => data.Trinkets.First(),
            _ => throw new ArgumentOutOfRangeException(nameof(resource))
        };

        AssertJson(expected, await Item(controller, resource));
        AssertJson(expected, await Item(controller, resource));
        AssertJson(expected, await Item(Controller(), resource));
    }

    [Theory]
    [InlineData("spec")]
    [InlineData("boss")]
    [InlineData("zone")]
    [InlineData("season")]
    [InlineData("spell")]
    [InlineData("trinket")]
    [InlineData("class-specs")]
    [InlineData("role-specs")]
    [InlineData("zone-bosses")]
    [InlineData("spec-spells")]
    [InlineData("boss-spells")]
    public async Task UnknownCatalogItemsReturnNotFoundInsteadOfEmptySuccess(string resource)
    {
        var controller = Controller();
        var response = resource switch
        {
            "class-specs" => await controller.GetClassSpecs("missing"),
            "role-specs" => await controller.GetRoleSpecs("missing"),
            "zone-bosses" => await controller.GetZoneBosses("missing"),
            "spec-spells" => controller.GetSpecSpells("missing"),
            "boss-spells" => controller.GetBossSpells("missing"),
            _ => await Item(controller, resource, missing: true)
        };
        var error = JsonSerializer.SerializeToElement(Assert.IsType<NotFoundObjectResult>(response).Value);
        Assert.Contains("not found", error.GetProperty("error").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("class-specs")]
    [InlineData("role-specs")]
    [InlineData("zone-bosses")]
    public async Task RelationshipEndpointsReturnOnlyMatchingItemsAndPersistThem(string resource)
    {
        var controller = Controller();
        object expected = resource switch
        {
            "class-specs" => new { specs = data.Specs.Where(spec => spec.ClassId == 2).OrderBy(spec => spec.Name) },
            "role-specs" => new { specs = data.Specs.Where(spec => spec.RoleId == 2).OrderBy(spec => spec.FullName) },
            "zone-bosses" => new { bosses = data.Bosses.Where(boss => boss.RaidId == data.Zones.First().Id) },
            _ => throw new ArgumentOutOfRangeException(nameof(resource))
        };
        AssertJson(expected, await Relationship(controller, resource));
        AssertJson(expected, await Relationship(controller, resource));
        AssertJson(expected, await Relationship(Controller(), resource));
    }

    [Fact]
    public void SpellRelationshipEndpointsReturnExplicitEmptyCollectionsForKnownEntities()
    {
        var controller = Controller();
        AssertJson(new { spells = Array.Empty<WowSpell>() }, controller.GetSpecSpells(data.Specs.First().FullNameSlug));
        AssertJson(new { spells = Array.Empty<WowSpell>() }, controller.GetBossSpells(data.Bosses.First().NameSlug));
    }

    private WorldDataController Controller() =>
        new(NullLogger<WorldDataController>.Instance, fixture.CreateService());

    private static Task<IActionResult> Collection(WorldDataController controller, string resource) => resource switch
    {
        "roles" => controller.GetRoles(),
        "classes" => controller.GetClasses(),
        "bosses" => controller.GetBosses(),
        "zones" => controller.GetZones(),
        "spells" => controller.GetSpells(),
        "trinkets" => controller.GetTrinkets(),
        "seasons" => controller.GetSeasons(),
        _ => throw new ArgumentOutOfRangeException(nameof(resource))
    };

    private Task<IActionResult> Relationship(WorldDataController controller, string resource) => resource switch
    {
        "class-specs" => controller.GetClassSpecs("paladin"),
        "role-specs" => controller.GetRoleSpecs("healer"),
        "zone-bosses" => controller.GetZoneBosses(data.Zones.First().Slug),
        _ => throw new ArgumentOutOfRangeException(nameof(resource))
    };

    private Task<IActionResult> Item(WorldDataController controller, string resource, bool missing = false) => resource switch
    {
        "spec" => controller.GetSpec(missing ? "missing" : data.Specs.First().FullNameSlug),
        "boss" => controller.GetBoss(missing ? "missing" : data.Bosses.First().NameSlug),
        "zone" => controller.GetZone(missing ? "missing" : data.Zones.First().Slug),
        "season" => controller.GetSeason(missing ? "missing" : data.Seasons.First().Slug),
        "spell" => controller.GetSpell(missing ? -1 : data.Spells.First().Id),
        "trinket" => controller.GetTrinket(missing ? -1 : data.Trinkets.First().Id),
        _ => throw new ArgumentOutOfRangeException(nameof(resource))
    };

    private static void AssertJson(object expected, IActionResult result)
    {
        var value = Assert.IsType<OkObjectResult>(result).Value;
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(value));
    }
}
