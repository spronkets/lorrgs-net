using Lorrgs.Api.Shared.Responses;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lorrgs.Api.Features.WorldData;
using Microsoft.Extensions.DependencyInjection;

namespace Lorrgs.Api.Tests.Features.WorldData;

public sealed class WorldDataEndpointTests(ApiEndpointFixture fixture)
    : IClassFixture<ApiEndpointFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly WorldDataService data = WorldDataService.Instance;

    [Fact]
    public async Task SwaggerDocumentsAllEndpointsAndTheirContracts()
    {
        var document = await fixture.Client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        var paths = document.GetProperty("paths");
        var worldDataPaths = paths.EnumerateObject().Where(path => path.Name.StartsWith("/api/WorldData/", StringComparison.Ordinal));
        Assert.Equal(19, worldDataPaths.Count());
        foreach (var path in worldDataPaths)
        {
            var operation = Assert.Single(path.Value.EnumerateObject()).Value;
            Assert.False(string.IsNullOrWhiteSpace(operation.GetProperty("summary").GetString()));
            var responses = operation.GetProperty("responses");
            Assert.True(responses.TryGetProperty("200", out _));
            Assert.True(responses.TryGetProperty("400", out _));
            var parameters = operation.GetProperty("parameters").EnumerateArray().ToArray();
            Assert.Contains(parameters, parameter => parameter.GetProperty("name").GetString() == "edition");
            foreach (var parameter in parameters.Where(parameter =>
                parameter.GetProperty("in").GetString() == "path"))
            {
                Assert.Contains($"{{{parameter.GetProperty("name").GetString()}}}", path.Name, StringComparison.Ordinal);
                Assert.True(parameter.GetProperty("required").GetBoolean());
            }
        }

        Assert.True(paths.GetProperty("/api/WorldData/bosses/{bossSlug}").GetProperty("get")
            .GetProperty("responses").TryGetProperty("404", out _));
        Assert.Contains(paths.EnumerateObject(), path => path.Name.StartsWith("/api/Rankings", StringComparison.OrdinalIgnoreCase));
        var specParameters = paths.GetProperty("/api/WorldData/specs").GetProperty("get").GetProperty("parameters").EnumerateArray().ToArray();
        Assert.Contains(specParameters, parameter => parameter.GetProperty("name").GetString() == "roleId");
        Assert.Contains(specParameters, parameter => parameter.GetProperty("name").GetString() == "classId");
        var schemas = document.GetProperty("components").GetProperty("schemas");
        Assert.True(schemas.GetProperty("GetBossesResponse").GetProperty("properties").TryGetProperty("bosses", out _));
        Assert.True(schemas.GetProperty("ApiErrorResponse").GetProperty("properties").TryGetProperty("error", out _));
        Assert.Same(data, fixture.App.Services.GetRequiredService<WorldDataService>());
    }

    [Fact]
    public async Task SwaggerUiRemainsAvailable()
    {
        using var response = await fixture.Client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("swagger-ui", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("roles")]
    [InlineData("classes")]
    [InlineData("bosses")]
    [InlineData("zones")]
    [InlineData("spells")]
    [InlineData("trinkets")]
    [InlineData("seasons")]
    public async Task CollectionsReturnOrderedCatalogs(string resource)
    {
        object expected = resource switch
        {
            "roles" => new { roles = data.Roles.OrderBy(item => item.Id) },
            "classes" => data.Classes.OrderBy(item => item.Name).ToDictionary(item => item.NameSlug),
            "bosses" => new { bosses = data.Bosses.OrderBy(item => item.Name) },
            "zones" => new { zones = data.Zones.OrderByDescending(item => item.Id) },
            "spells" => new { spells = data.Spells.OrderBy(item => item.Name) },
            "trinkets" => new { trinkets = data.Trinkets.OrderBy(item => item.Name) },
            "seasons" => new { seasons = data.Seasons.OrderBy(item => item.StartDate) },
            _ => throw new ArgumentOutOfRangeException(nameof(resource))
        };
        await AssertResponse(resource, expected);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(2, null)]
    [InlineData(null, 2)]
    [InlineData(2, 2)]
    [InlineData(-1, -1)]
    public async Task SpecsBindAndApplyCombinedQueryFilters(int? role, int? wowClass)
    {
        var query = (role.HasValue ? $"&roleId={role}" : "") +
            (wowClass.HasValue ? $"&classId={wowClass}" : "");
        var expected = new
        {
            specs = data.Specs.Where(spec => (!role.HasValue || spec.RoleId == role) &&
                (!wowClass.HasValue || spec.ClassId == wowClass)).OrderBy(spec => spec.FullName)
        };
        await AssertResponse("specs", expected, query);
    }

    [Theory]
    [InlineData("spec")]
    [InlineData("boss")]
    [InlineData("zone")]
    [InlineData("season")]
    [InlineData("spell")]
    [InlineData("trinket")]
    public async Task ItemsReturnMatchingCatalogDefinition(string resource)
    {
        var (route, expected) = Item(resource);
        await AssertResponse(route, expected);
    }

    [Fact]
    public async Task BossLookupReturnsCurrentServerDefinition()
    {
        var result = await fixture.Client.GetFromJsonAsync<JsonElement>(
            "/api/worlddata/bosses/nalorakk?edition=anniversary");
        Assert.Equal("ui-ej-boss-nalorakk.png", result.GetProperty("icon").GetString());
    }

    [Theory]
    [InlineData("specs/missing")]
    [InlineData("bosses/missing")]
    [InlineData("zones/missing")]
    [InlineData("seasons/missing")]
    [InlineData("spells/-1")]
    [InlineData("trinkets/-1")]
    [InlineData("classes/missing/specs")]
    [InlineData("roles/missing/specs")]
    [InlineData("zones/missing/bosses")]
    [InlineData("specs/missing/spells")]
    [InlineData("bosses/missing/spells")]
    public async Task MissingItemsAndRelationshipsReturnNotFound(string route)
    {
        using var response = await fixture.Client.GetAsync($"/api/WorldData/{route}?edition=retail");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Contains("not found", Assert.IsType<ApiErrorResponse>(error).Error,
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("class-specs")]
    [InlineData("role-specs")]
    [InlineData("zone-bosses")]
    public async Task RelationshipsReturnOnlyMatchingItems(string resource)
    {
        var route = resource switch
        {
            "class-specs" => "classes/paladin/specs",
            "role-specs" => "roles/healer/specs",
            "zone-bosses" => $"zones/{data.Zones.First().Slug}/bosses",
            _ => throw new ArgumentOutOfRangeException(nameof(resource))
        };
        object expected = resource switch
        {
            "class-specs" => new { specs = data.Specs.Where(spec => spec.ClassId == 2).OrderBy(spec => spec.Name) },
            "role-specs" => new { specs = data.Specs.Where(spec => spec.RoleId == 2).OrderBy(spec => spec.FullName) },
            _ => new { bosses = data.Bosses.Where(boss => boss.RaidId == data.Zones.First().Id) }
        };
        await AssertResponse(route, expected);
    }

    [Fact]
    public async Task KnownEntitiesHaveExplicitEmptySpellCollections()
    {
        await AssertResponse($"specs/{data.Specs.First().FullNameSlug}/spells", new { spells = Array.Empty<object>() });
        await AssertResponse($"bosses/{data.Bosses.First().NameSlug}/spells", new { spells = Array.Empty<object>() });
    }

    [Theory]
    [InlineData("Anniversary")]
    [InlineData(" Era ")]
    [InlineData("classic")]
    [InlineData("Retail")]
    [InlineData("midnight")]
    [InlineData("Mists of Pandaria")]
    [InlineData("mop")]
    [InlineData("classic_sod")]
    public async Task EditionAliasesAreAcceptedByThePipeline(string edition)
    {
        using var response = await fixture.Client.GetAsync(
            $"/api/WorldData/roles?edition={Uri.EscapeDataString(edition)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("../retail")]
    public async Task EditionValidationAppliesToEveryEndpoint(string? edition)
    {
        var routes = new[]
        {
            "roles", "classes", "specs", Item("spec").Route, "classes/paladin/specs",
            "roles/healer/specs", "bosses", Item("boss").Route, "zones", Item("zone").Route,
            $"zones/{data.Zones.First().Slug}/bosses", Item("spell").Route, Item("trinket").Route,
            $"specs/{data.Specs.First().FullNameSlug}/spells",
            $"bosses/{data.Bosses.First().NameSlug}/spells", "spells", "trinkets", "seasons", Item("season").Route
        };
        foreach (var route in routes)
        {
            var query = edition is null ? "" : $"?edition={Uri.EscapeDataString(edition)}";
            using var response = await fixture.Client.GetAsync($"/api/WorldData/{route}{query}");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
            Assert.Equal("A supported edition query parameter is required.", error?.Error);
        }
    }

    [Theory]
    [InlineData("specs?roleId=invalid")]
    [InlineData("specs?classId=invalid")]
    [InlineData("spells/invalid?")]
    [InlineData("trinkets/invalid?")]
    public async Task InvalidNumericInputReturnsBadRequest(string route)
    {
        using var response = await fixture.Client.GetAsync($"/api/WorldData/{route}&edition=retail");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private (string Route, object Expected) Item(string resource) => resource switch
    {
        "spec" => ($"specs/{data.Specs.First().FullNameSlug}", data.Specs.First()),
        "boss" => ($"bosses/{data.Bosses.First().NameSlug}", data.Bosses.First()),
        "zone" => ($"zones/{data.Zones.First().Slug}", data.Zones.First()),
        "season" => ($"seasons/{data.Seasons.First().Slug}", data.Seasons.First()),
        "spell" => ($"spells/{data.Spells.First().Id}", data.Spells.First()),
        "trinket" => ($"trinkets/{data.Trinkets.First().Id}", data.Trinkets.First()),
        _ => throw new ArgumentOutOfRangeException(nameof(resource))
    };

    private async Task AssertResponse(string route, object expected, string query = "")
    {
        using var response = await fixture.Client.GetAsync($"/api/WorldData/{route}?edition=retail{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var actual = await response.Content.ReadFromJsonAsync<JsonNode>();
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected, JsonOptions), actual),
            $"Unexpected response for {route}: {actual}");
    }
}
