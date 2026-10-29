using System.Text.Json;
using Lorrgs.Api.Controllers;
using Lorrgs.Api.Models;
using Lorrgs.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;

namespace Lorrgs.Api.Tests;

public sealed class WorldDataControllerTests
{
    private readonly WorldDataService data = WorldDataService.Instance;

    [Fact]
    public async Task SwaggerGenerationExcludesActionFilterCallbacks()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(WorldDataController).Assembly);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "Test API", Version = "v1" }));
        await using var app = builder.Build();
        app.MapControllers();

        var document = app.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");

        Assert.Contains("/api/WorldData/roles", document.Paths.Keys);
        Assert.DoesNotContain(document.Paths.Keys,
            path => path.Contains("OnAction", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BossLookupReturnsCurrentServerDefinition()
    {
        var boss = data.GetBoss("nalorakk");
        var response = Assert.IsType<OkObjectResult>(Controller("anniversary").GetBoss("nalorakk"));
        Assert.Same(boss, response.Value);
        Assert.Equal("ui-ej-boss-nalorakk.png", Assert.IsType<RaidBoss>(response.Value).Icon);
    }

    [Theory]
    [InlineData("roles")]
    [InlineData("classes")]
    [InlineData("bosses")]
    [InlineData("zones")]
    [InlineData("spells")]
    [InlineData("trinkets")]
    [InlineData("seasons")]
    public void CollectionEndpointsReturnOrderedServerCatalogs(string resource)
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

        AssertJson(expected, Collection(controller, resource));
        AssertJson(expected, Collection(controller, resource));
        AssertJson(expected, Collection(Controller(), resource));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(2, null)]
    [InlineData(null, 2)]
    [InlineData(2, 2)]
    [InlineData(-1, -1)]
    public void GetSpecsAppliesCombinedFilters(int? role, int? wowClass)
    {
        var controller = Controller();
        var expected = new
        {
            specs = data.Specs.Where(spec => (!role.HasValue || spec.RoleId == role) &&
                (!wowClass.HasValue || spec.ClassId == wowClass)).OrderBy(spec => spec.FullName)
        };

        AssertJson(expected, controller.GetSpecs(role, wowClass));
        AssertJson(expected, Controller().GetSpecs(role, wowClass));
        AssertJson(new { specs = data.Specs.OrderBy(spec => spec.FullName) }, controller.GetSpecs());
    }

    [Theory]
    [InlineData("spec")]
    [InlineData("boss")]
    [InlineData("zone")]
    [InlineData("season")]
    [InlineData("spell")]
    [InlineData("trinket")]
    public void ItemEndpointsReturnMatchingServerCatalogItem(string resource)
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

        AssertJson(expected, Item(controller, resource));
        AssertJson(expected, Item(controller, resource));
        AssertJson(expected, Item(Controller(), resource));
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
    public void UnknownCatalogItemsReturnNotFoundInsteadOfEmptySuccess(string resource)
    {
        var controller = Controller();
        var response = resource switch
        {
            "class-specs" => controller.GetClassSpecs("missing"),
            "role-specs" => controller.GetRoleSpecs("missing"),
            "zone-bosses" => controller.GetZoneBosses("missing"),
            "spec-spells" => controller.GetSpecSpells("missing"),
            "boss-spells" => controller.GetBossSpells("missing"),
            _ => Item(controller, resource, missing: true)
        };
        var error = JsonSerializer.SerializeToElement(Assert.IsType<NotFoundObjectResult>(response).Value);
        Assert.Contains("not found", error.GetProperty("error").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("class-specs")]
    [InlineData("role-specs")]
    [InlineData("zone-bosses")]
    public void RelationshipEndpointsReturnOnlyMatchingItems(string resource)
    {
        var controller = Controller();
        object expected = resource switch
        {
            "class-specs" => new { specs = data.Specs.Where(spec => spec.ClassId == 2).OrderBy(spec => spec.Name) },
            "role-specs" => new { specs = data.Specs.Where(spec => spec.RoleId == 2).OrderBy(spec => spec.FullName) },
            "zone-bosses" => new { bosses = data.Bosses.Where(boss => boss.RaidId == data.Zones.First().Id) },
            _ => throw new ArgumentOutOfRangeException(nameof(resource))
        };
        AssertJson(expected, Relationship(controller, resource));
        AssertJson(expected, Relationship(controller, resource));
        AssertJson(expected, Relationship(Controller(), resource));
    }

    [Fact]
    public void SpellRelationshipEndpointsReturnExplicitEmptyCollectionsForKnownEntities()
    {
        var controller = Controller();
        AssertJson(new { spells = Array.Empty<WowSpell>() }, controller.GetSpecSpells(data.Specs.First().FullNameSlug));
        AssertJson(new { spells = Array.Empty<WowSpell>() }, controller.GetBossSpells(data.Bosses.First().NameSlug));
    }

    [Theory]
    [InlineData("Anniversary", "anniversary")]
    [InlineData(" Era ", "era")]
    [InlineData("classic", "era")]
    [InlineData("Retail", "retail")]
    [InlineData("midnight", "retail")]
    [InlineData("Mists of Pandaria", "mistsofpandaria")]
    [InlineData("mop", "mistsofpandaria")]
    [InlineData("classic_sod", "classic_sod")]
    public void EditionAliasesAreNormalized(string edition, string normalized)
    {
        var controller = Controller(edition);
        var context = new ActionExecutingContext(
            new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
            [], new Dictionary<string, object?>(), controller);

        controller.OnActionExecuting(context);

        Assert.Null(context.Result);
        Assert.Equal(normalized, controller.Edition);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("../retail")]
    public void MissingOrUnsupportedEditionsAreBadRequests(string? edition)
    {
        var controller = Controller(edition);
        var context = new ActionExecutingContext(
            new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
            [], new Dictionary<string, object?>(), controller);

        controller.OnActionExecuting(context);

        Assert.IsType<BadRequestObjectResult>(context.Result);
    }

    private WorldDataController Controller(string? edition = "retail") =>
        new(NullLogger<WorldDataController>.Instance)
        { Edition = edition };

    private static IActionResult Collection(WorldDataController controller, string resource) => resource switch
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

    private IActionResult Relationship(WorldDataController controller, string resource) => resource switch
    {
        "class-specs" => controller.GetClassSpecs("paladin"),
        "role-specs" => controller.GetRoleSpecs("healer"),
        "zone-bosses" => controller.GetZoneBosses(data.Zones.First().Slug),
        _ => throw new ArgumentOutOfRangeException(nameof(resource))
    };

    private IActionResult Item(WorldDataController controller, string resource, bool missing = false) => resource switch
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
