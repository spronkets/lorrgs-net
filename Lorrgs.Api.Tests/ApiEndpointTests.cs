using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Lorrgs.Api.Features.WarcraftLogs.AnalyzeRotation;
using Lorrgs.Tests.TestSupport;
using Lorrgs.WarcraftLogs.Models;
using Lorrgs.WarcraftLogs.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Lorrgs.Api.Tests;

public sealed class ApiEndpointTests(ApiEndpointFixture fixture) : IClassFixture<ApiEndpointFixture>
{
    [Theory]
    [InlineData("/api/warcraftlogs/reports/bad-code/timeline?fightId=1&sourceId=7&edition=retail", HttpStatusCode.BadRequest)]
    [InlineData("/api/route-that-does-not-exist", HttpStatusCode.NotFound)]
    [InlineData("/api/WorldData/roles?edition=retail", HttpStatusCode.OK)]
    public async Task ResponsesExposeServerCorrelationId(string url, HttpStatusCode status)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Correlation-ID", "unverified-client-value");
        using var response = await fixture.Client.SendAsync(request);
        Assert.Equal(status, response.StatusCode);
        var correlationId = Assert.Single(response.Headers.GetValues("X-Correlation-ID"));
        Assert.False(string.IsNullOrWhiteSpace(correlationId));
        Assert.NotEqual("unverified-client-value", correlationId);
    }

    [Fact]
    public async Task RootCompositionDocumentsAll34RoutesExactlyOnce()
    {
        var document = await fixture.Client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        var paths = document.GetProperty("paths");
        string[] expected =
        [
            "GET /api/WorldData/roles", "GET /api/WorldData/classes",
            "GET /api/WorldData/specs", "GET /api/WorldData/specs/{specSlug}",
            "GET /api/WorldData/classes/{classSlug}/specs", "GET /api/WorldData/roles/{roleSlug}/specs",
            "GET /api/WorldData/bosses", "GET /api/WorldData/bosses/{bossSlug}",
            "GET /api/WorldData/zones", "GET /api/WorldData/zones/{zoneSlug}",
            "GET /api/WorldData/zones/{zoneSlug}/bosses", "GET /api/WorldData/spells/{spellId}",
            "GET /api/WorldData/trinkets/{trinketId}", "GET /api/WorldData/specs/{specSlug}/spells",
            "GET /api/WorldData/bosses/{bossSlug}/spells", "GET /api/WorldData/spells",
            "GET /api/WorldData/trinkets", "GET /api/WorldData/seasons",
            "GET /api/WorldData/seasons/{seasonSlug}",
            "GET /api/Rankings/spec/{specSlug}/{bossSlug}",
            "GET /api/Rankings/spec/{specSlug}/{bossSlug}/info",
            "POST /api/Rankings/spec/queue", "POST /api/Rankings/comp/queue",
            "PATCH /api/Rankings/spec/dirty",
            "GET /api/rankings/comp/{bossSlug}",
            "GET /api/rankings/comp/reports/{code}/fights/{fightId}",
            "GET /api/warcraftlogs/reports/{reportCode}/timeline",
            "GET /api/warcraftlogs/reports/{reportCode}/timeline/boss",
            "GET /api/WarcraftLogs/seasons", "GET /api/WarcraftLogs/rankings",
            "GET /api/WarcraftLogs/reports/{reportId}",
            "GET /api/WarcraftLogs/reports/{reportCode}/lookup",
            "POST /api/WarcraftLogs/rotation/analyze", "GET /api/raid-catalog"
        ];
        var actual = paths.EnumerateObject().SelectMany(path => path.Value.EnumerateObject()
            .Select(operation => $"{operation.Name.ToUpperInvariant()} {path.Name}")).ToArray();
        Assert.Equal(expected.Order(), actual.Order());
        var operationIds = paths.EnumerateObject().SelectMany(path => path.Value.EnumerateObject())
            .Select(operation => operation.Value.GetProperty("operationId").GetString()).ToArray();
        Assert.Equal(34, operationIds.Distinct().Count());
        Assert.DoesNotContain(actual, route => route.Contains("/api/api/", StringComparison.Ordinal));

        var ranking = paths.GetProperty("/api/Rankings/spec/{specSlug}/{bossSlug}").GetProperty("get");
        var difficulty = ranking.GetProperty("parameters").EnumerateArray()
            .Single(parameter => parameter.GetProperty("name").GetString() == "difficulty");
        Assert.False(difficulty.TryGetProperty("required", out var required) && required.GetBoolean());
        Assert.Equal("mythic", difficulty.GetProperty("schema").GetProperty("default").GetString());
        Assert.Equal(["200", "400", "404", "429", "500"],
            ranking.GetProperty("responses").EnumerateObject().Select(response => response.Name).Order());
        var analysis = paths.GetProperty("/api/WarcraftLogs/rotation/analyze").GetProperty("post");
        Assert.True(analysis.GetProperty("requestBody").GetProperty("required").GetBoolean());
    }

    [Theory]
    [InlineData("/api/WarcraftLogs/seasons", "id", "season-1")]
    [InlineData("/api/WarcraftLogs/rankings", "season", "current")]
    [InlineData("/api/WarcraftLogs/rankings?season=season-1", "season", "season-1")]
    public async Task ExistingListingsRemainAvailableWithoutEdition(string url, string property, string expected)
    {
        var data = await fixture.Client.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal(2, data.GetArrayLength());
        Assert.Equal(expected, data[0].GetProperty(property).GetString());
    }

    [Fact]
    public async Task ReportAndCatalogRemainAvailableWithoutEdition()
    {
        var report = await fixture.Client.GetFromJsonAsync<JsonElement>("/api/WarcraftLogs/reports/ReportA");
        Assert.Equal("ReportA", report.GetProperty("reportId").GetString());
        Assert.Equal("LorrgsNET", report.GetProperty("owner").GetString());
        var catalog = await fixture.Client.GetFromJsonAsync<JsonElement>("/api/raid-catalog");
        Assert.Equal(JsonValueKind.Array, catalog.GetProperty("editions").ValueKind);
    }

    [Theory]
    [InlineData("spec", "", "all", 50, false)]
    [InlineData("comp", "", "all", 50, false)]
    [InlineData("spec", "?bossSlug=eranog&limit=3&clear=true&specSlug=fire-mage&difficulty=heroic&metric=hps", "eranog", 3, true)]
    [InlineData("comp", "?bossSlug=eranog&limit=3&clear=true", "eranog", 3, true)]
    public async Task QueueRoutesPreserveDefaultsAndQueryPayload(
        string type, string query, string boss, int limit, bool clear)
    {
        using var response = await fixture.Client.PostAsync($"/api/Rankings/{type}/queue{query}", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(Guid.TryParse(data.GetProperty("taskId").GetString(), out _));
        var payload = data.GetProperty("payload");
        Assert.Equal(boss, payload.GetProperty("bossSlug").GetString());
        Assert.Equal(limit, payload.GetProperty("limit").GetInt32());
        Assert.Equal(clear, payload.GetProperty("clear").GetBoolean());
        if (type == "spec")
        {
            Assert.Equal(clear ? "fire-mage" : "all", payload.GetProperty("specSlug").GetString());
            Assert.Equal(clear ? "heroic" : "all", payload.GetProperty("difficulty").GetString());
            Assert.Equal(clear ? "hps" : "all", payload.GetProperty("metric").GetString());
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DirtyRouteBindsItsQueryAndPreservesResponse(bool dirty)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch,
            "/api/Rankings/spec/dirty?specSlug=fire-mage&bossSlug=eranog&edition=retail" +
            $"&zoneId=38&className=Mage&specName=Fire&metric=dps&dirty={dirty}");
        using var response = await fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("fire-mage", data.GetProperty("specSlug").GetString());
        Assert.Equal("mythic", data.GetProperty("difficulty").GetString());
        Assert.Contains(dirty ? "refresh" : "clean", data.GetProperty("message").GetString());
        Assert.Equal(0, fixture.Upstream.GraphQlRequests);
    }

    [Theory]
    [InlineData("/api/Rankings/spec/fire-mage/eranog")]
    [InlineData("/api/Rankings/spec/fire-mage/eranog/info")]
    public async Task RankingValidationKeepsSharedErrorContract(string url)
    {
        using var response = await fixture.Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("edition", data.GetProperty("error").GetString());
        Assert.Equal(0, fixture.Upstream.GraphQlRequests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/info")]
    public async Task RankingRoutesBindDefaultsAndForwardAllQueryOptions(string suffix)
    {
        const string rankings = """
            {"data":{"worldData":{"encounter":{"id":2898,"name":"Eranog",
            "characterRankings":{"page":2,"count":0,"hasMorePages":false,"rankings":[]}}}}}
            """;
        await using var host = new ApiEndpointFixture
        {
            Upstream = new QueueHandler(rankings, rankings)
        };
        await host.InitializeAsync();
        var data = await host.Client.GetFromJsonAsync<JsonElement>(
            $"/api/Rankings/spec/fire-mage/eranog{suffix}?edition=retail&zoneId=38&encounterId=2898" +
            "&className=Mage&specName=Fire&metric=dps&page=2&partition=1&bracket=2&size=20" +
            "&serverRegion=US&serverSlug=area-52&filter=test&hardModeLevel=any&externalBuffs=exclude");
        Assert.Equal("fire-mage", data.GetProperty("specSlug").GetString());
        Assert.Equal("mythic", data.GetProperty("difficulty").GetString());
        Assert.Empty(data.GetProperty("reports").EnumerateArray());
        Assert.Equal(2, host.Upstream.GraphQlRequests);
        var variables = host.Upstream.Requests[0].GetProperty("variables");
        Assert.Equal(2, variables.GetProperty("page").GetInt32());
        Assert.Equal(20, variables.GetProperty("size").GetInt32());
        Assert.Equal(JsonValueKind.Null, host.Upstream.Requests[1].GetProperty("variables").GetProperty("size").ValueKind);
        Assert.Equal(5, variables.GetProperty("difficulty").GetInt32());
        Assert.Equal("US", variables.GetProperty("serverRegion").GetString());
        Assert.Equal("area-52", variables.GetProperty("serverSlug").GetString());
        Assert.Equal("test", variables.GetProperty("filter").GetString());
        Assert.Equal("Any", variables.GetProperty("hardModeLevel").GetString());
        Assert.Equal("Exclude", variables.GetProperty("externalBuffs").GetString());
        Assert.True(variables.GetProperty("includeCombatantInfo").GetBoolean());
        Assert.True(variables.GetProperty("includeOtherPlayers").GetBoolean());
    }

    [Theory]
    [InlineData("/api/Rankings/spec/fire-mage/eranog?refresh=invalid")]
    [InlineData("/api/rankings/comp/boss?page=invalid")]
    [InlineData("/api/warcraftlogs/reports/ReportA/timeline?fightId=invalid")]
    public async Task MalformedQueryValuesReturnBadRequest(string url)
    {
        using var response = await fixture.Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, fixture.Upstream.GraphQlRequests);
    }

    [Theory]
    [InlineData("/api/rankings/comp/boss", "title")]
    [InlineData("/api/rankings/comp/reports/ReportA/fights/1", "title")]
    [InlineData("/api/warcraftlogs/reports/ReportA/timeline", "detail")]
    [InlineData("/api/warcraftlogs/reports/ReportA/timeline/boss", "detail")]
    public async Task CompositionAndTimelineKeepProblemDetailsValidation(string url, string property)
    {
        using var response = await fixture.Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, data.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(data.GetProperty(property).GetString()));
        Assert.Equal(0, fixture.Upstream.GraphQlRequests);
    }

    [Fact]
    public async Task CompositionSearchResolvesUnmappedBossFromZoneOverHttp()
    {
        await using var host = new ApiEndpointFixture
        {
            Upstream = new QueueHandler(
                """{"data":{"worldData":{"zone":{"encounters":[{"id":12345,"name":"Archimonde"}]}}}}""",
                """{"data":{"worldData":{"encounter":{"name":"Archimonde","fightRankings":{"hasMorePages":false,"rankings":[]}}}}}""")
        };
        await host.InitializeAsync();
        using var response = await host.Client.GetAsync(
            "/api/rankings/comp/archimonde?edition=Anniversary&zoneId=1060&difficultyId=3");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("archimonde", data.GetProperty("bossSlug").GetString());
        Assert.Equal(2, host.Upstream.GraphQlRequests);
        Assert.Equal(1060, host.Upstream.Requests[0].GetProperty("variables").GetProperty("zoneId").GetInt32());
        Assert.Equal(12345, host.Upstream.Requests[1].GetProperty("variables").GetProperty("encounterId").GetInt32());
    }

    [Fact]
    public async Task CompositionRoutesBindRepeatedFiltersAliasesAndDetailParameters()
    {
        var service = new CompositionService();
        await using var host = new ApiEndpointFixture
        {
            ConfigureServices = services => services.AddSingleton<ICompRankingService>(service)
        };
        await host.InitializeAsync();
        var ranking = await host.Client.GetFromJsonAsync<JsonElement>(
            "/api/rankings/comp/boss?edition=mop&encounterId=123&page=2&refresh=true" +
            "&role=tank.eq.2&role=heal.gte.3&spec=fire-mage.lte.3&spec=holy-paladin.lte.1" +
            "&killtime_min=60&killtime_max=180");
        Assert.Equal("boss", ranking.GetProperty("bossSlug").GetString());
        var search = Assert.IsType<CompRankingSearch>(service.Search);
        Assert.Equal("mistsofpandaria", search.Edition);
        Assert.Equal(123, search.EncounterId);
        Assert.Equal(5, search.Difficulty);
        Assert.Equal(2, search.Page);
        Assert.True(search.Refresh);
        Assert.Equal((60, 180), (search.MinSeconds, search.MaxSeconds));
        Assert.Equal(["tank", "heal"], search.Roles.Select(role => role.Key));
        Assert.Equal(["fire-mage", "holy-paladin"], search.Specs.Select(spec => spec.Key));
        Assert.True(service.Cancellation.CanBeCanceled);
        await host.Client.GetFromJsonAsync<JsonElement>(
            "/api/rankings/comp/reports/ReportA/fights/7?edition=classic&refresh=true");
        Assert.Equal(("ReportA", 7, "era", true), service.Details);
        Assert.True(service.Cancellation.CanBeCanceled);
    }

    [Theory]
    [InlineData("", null, "Player", 264)]
    [InlineData("&sourceId=7", 7, null, null)]
    public async Task TimelineRoutesKeepDefaultEditionAndForwardPlayerAndBossSelections(
        string query, int? actor, string? player, int? spec)
    {
        var service = new TimelineService();
        await using var host = new ApiEndpointFixture
        {
            ConfigureServices = services => services.AddSingleton<IReportTimelineService>(service)
        };
        await host.InitializeAsync();
        var selection = actor.HasValue ? query : $"&playerName={player}&specId={spec}";
        await host.Client.GetFromJsonAsync<JsonElement>(
            $"/api/warcraftlogs/reports/ReportA/timeline?fightId=3{selection}");
        Assert.Equal(("ReportA", 3, actor, player, "retail", spec), service.Player);
        Assert.True(service.Cancellation.CanBeCanceled);
        await host.Client.GetFromJsonAsync<JsonElement>(
            "/api/warcraftlogs/reports/ReportA/timeline/boss?fightId=3");
        Assert.Equal(("ReportA", 3, "retail", false), service.Boss);
        using var invalid = await host.Client.GetAsync(
            "/api/warcraftlogs/reports/ReportA/timeline/boss?fightId=3&edition=midnight");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{invalid}")]
    public async Task AnalysisRejectsMissingOrMalformedBodies(string body)
    {
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await fixture.Client.PostAsync("/api/WarcraftLogs/rotation/analyze", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, fixture.Upstream.GraphQlRequests);
    }

    [Fact]
    public async Task LookupAndAnalysisBindRouteAndBodyToTheirServices()
    {
        await using var host = new ApiEndpointFixture
        {
            Upstream = new QueueHandler(
                """{"data":{"reportData":{"report":{"title":"Raid"}}}}""",
                """
                {"data":{"reportData":{"report":{"fights":[{"id":1,"name":"Boss","startTime":0,"endTime":60000}],
                "masterData":{"actors":[{"id":7,"name":"Player","subType":"Holy"}]}}}}}
                """, """{"data":{"reportData":{"report":{"events":{"data":[]}}}}}""")
        };
        await host.InitializeAsync();
        var lookup = await host.Client.GetFromJsonAsync<JsonElement>("/api/WarcraftLogs/reports/ReportA/lookup");
        Assert.Equal("Raid", lookup.GetProperty("reportData").GetProperty("report").GetProperty("title").GetString());
        using var response = await host.Client.PostAsJsonAsync("/api/WarcraftLogs/rotation/analyze",
            new RotationAnalysisRequest { ReportCode = "ReportB", FightId = 1, SourceId = 7 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var analysis = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Player", analysis.GetProperty("playerName").GetString());
        Assert.Equal("ReportB", analysis.GetProperty("reportCode").GetString());
        Assert.Equal(3, host.Upstream.GraphQlRequests);
    }

    private sealed class CompositionService : ICompRankingService
    {
        public CompRankingSearch? Search { get; private set; }
        public (string, int, string, bool)? Details { get; private set; }
        public CancellationToken Cancellation { get; private set; }

        public Task<CompRankingData?> LoadAsync(CompRankingSearch search, CancellationToken cancellationToken)
        {
            Search = search;
            Cancellation = cancellationToken;
            return Task.FromResult<CompRankingData?>(new() { BossSlug = search.BossSlug });
        }

        public Task<RankingReport> LoadDetailsAsync(string code, int fightId, string edition,
            bool refresh, CancellationToken cancellationToken)
        {
            Details = (code, fightId, edition, refresh);
            Cancellation = cancellationToken;
            return Task.FromResult(new RankingReport { ReportId = code, Title = code });
        }
    }

    private sealed class TimelineService : IReportTimelineService
    {
        public (string, int, int?, string?, string, int?)? Player { get; private set; }
        public (string, int, string, bool)? Boss { get; private set; }
        public CancellationToken Cancellation { get; private set; }

        public Task<ReportTimeline> LoadAsync(string reportCode, int fightId, int? sourceId, string? playerName,
            string edition, CancellationToken cancellationToken, int? specId = null)
        {
            Player = (reportCode, fightId, sourceId, playerName, edition, specId);
            Cancellation = cancellationToken;
            return Task.FromResult(new ReportTimeline(reportCode, fightId, sourceId ?? 7, playerName ?? "Player",
                60000, false, [], []));
        }

        public Task<BossTimeline?> LoadBossAsync(string reportCode, int fightId, string edition, bool cacheOnly,
            CancellationToken cancellationToken)
        {
            Boss = (reportCode, fightId, edition, cacheOnly);
            Cancellation = cancellationToken;
            return Task.FromResult<BossTimeline?>(new(reportCode, fightId, "Boss", 60000, false, [], [], []));
        }

        public Task PopulateRosterTimelinesAsync(RankingReport report, string edition, bool refresh,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ReportTimeline?> TryLoadCachedAsync(string reportCode, int fightId, int? sourceId,
            string? playerName, string edition, CancellationToken cancellationToken, int? specId = null) =>
            throw new NotSupportedException();
        public Task PopulateFightPhasesAsync(IReadOnlyList<RankingReport> reports, string edition,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
