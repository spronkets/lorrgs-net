using System.Net;
using System.Text.Json;
using Lorrgs.Api.Features.CompRankings.GetCompRanking;
using Lorrgs.Api.Features.CompRankings.GetDetails;
using Lorrgs.Api.Features.WorldData;
using Lorrgs.Tests.TestSupport;
using Lorrgs.WarcraftLogs.Models;
using Lorrgs.WarcraftLogs.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lorrgs.Api.Tests;

public sealed class CompRankingsTests : IDisposable
{
    private readonly TestClientFixture fixture = new();
    public void Dispose() => fixture.Dispose();

    [Theory]
    [InlineData("heal.eq.2", 2, true)]
    [InlineData("heal.lt.2", 1, true)]
    [InlineData("heal.gt.2", 2, false)]
    [InlineData("heal.lte.2", 2, true)]
    [InlineData("heal.gte.2", 2, true)]
    [InlineData("absent.eq.0", 5, true)]
    [InlineData("heal.2", 2, true)]
    public void CountExpressionsCompareIncludingAbsentKeys(string expression, int healers, bool expected) =>
        Assert.Equal(expected, CompCountFilter.Parse(expression).Matches(new Dictionary<string, int> { ["heal"] = healers }));

    [Theory]
    [InlineData("heal.eq.-1")]
    [InlineData("heal.no.2")]
    [InlineData("heal.eq.2.trailing")]
    [InlineData("heal.eq.2\n")]
    [InlineData("heal.eq.101")]
    public void CountExpressionsRejectMalformedInputs(string expression) =>
        Assert.Throws<ArgumentException>(() => CompCountFilter.Parse(expression));

    [Fact]
    public async Task UnmappedArchimondeResolvesFromSelectedZoneAndReusesCachedLookup()
    {
        using var handler = new QueueHandler(
            """{"data":{"worldData":{"zone":{"encounters":[{"id":999,"name":"Other boss"},{"id":12345,"name":"Archimonde"}]}}}}""",
            Rankings(1).Replace("Illidan", "Archimonde"));
        var handlers = Handlers(handler);
        var request = new GetCompRankingRequest("archimonde", "anniversary", DifficultyId: 3, ZoneId: 1060);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var response = await handlers.Ranking.Handle(request);
            Assert.Equal(200, HttpResultAssertions.Read(response).StatusCode);
            var ranking = Assert.IsType<CompRankingData>(HttpResultAssertions.Read(response).Value);
            Assert.Equal("archimonde", ranking.BossSlug);
            Assert.Equal(12345, Assert.Single(Assert.Single(ranking.Reports).Fights).EncounterId);
        }
        Assert.Equal(2, handler.GraphQlRequests);
        Assert.Equal(1060, handler.Requests[0].GetProperty("variables").GetProperty("zoneId").GetInt32());
        Assert.Equal(12345, handler.Requests[1].GetProperty("variables").GetProperty("encounterId").GetInt32());
    }

    [Fact]
    public async Task MissingEncounterNameReturnsNotFoundWithoutQueryingRankings()
    {
        using var handler = new QueueHandler(
            """{"data":{"worldData":{"zone":{"encounters":[{"id":999,"name":"Other boss"}]}}}}""");
        var response = await Handlers(handler).Ranking.Handle(
            new GetCompRankingRequest("archimonde", "anniversary", ZoneId: 1060));
        Assert.Equal(404, HttpResultAssertions.Read(response).StatusCode);
        Assert.Equal(1, handler.GraphQlRequests);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 1060)]
    [InlineData(0, -1)]
    public async Task MissingOrNegativeEncounterContextReturnsBadRequest(int encounterId, int zoneId)
    {
        using var handler = new QueueHandler();
        var response = await Handlers(handler).Ranking.Handle(
            new GetCompRankingRequest("archimonde", "anniversary", encounterId, ZoneId: zoneId));
        Assert.Equal(400, HttpResultAssertions.Read(response).StatusCode);
        Assert.Equal(0, handler.GraphQlRequests);
    }

    [Fact]
    public async Task CompletePageReturnsAll50SummariesWithExactlyOneUpstreamCallEvenWithCompositionFilters()
    {
        using var handler = new QueueHandler(Rankings(Enumerable.Range(1, 50).Reverse().ToArray()));
        var result = (await Service(handler).LoadAsync(Search() with
        {
            Roles = [CompCountFilter.Parse("heal.eq.0")],
            MinSeconds = 600
        }, default))!;
        Assert.Equal(50, result.Reports.Count);
        Assert.All(result.Reports, report =>
        {
            Assert.Empty(report.Players);
            Assert.Null(report.Fights[0].Composition);
            Assert.Equal(2, report.Fights[0].RoleCounts!["tank"]);
            Assert.Equal(4, report.Fights[0].RoleCounts!["heal"]);
            Assert.Equal(9, report.Fights[0].RoleCounts!["mdps"]);
            Assert.Equal(10, report.Fights[0].RoleCounts!["rdps"]);
            Assert.Null(report.BossTimeline);
        });
        Assert.Equal(1, handler.GraphQlRequests);
        Assert.True(result.HasMorePages);
        Assert.Equal(Enumerable.Range(1, 50).Reverse(),
            result.Reports.Select(report => report.Fights[0].FightId));
        Assert.Contains("metric: execution", handler.Requests[0].GetProperty("query").GetString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DetailRequestsShareHandlerCancellation(bool cancel)
    {
        var roster = System.Text.Json.Nodes.JsonNode.Parse(Roster(1))!;
        roster["data"]!["reportData"]!["report"]!["healingRankings"] =
            System.Text.Json.Nodes.JsonNode.Parse("""
                {"data":[{"fightID":1,"roles":{"healers":{"characters":[
                {"id":123,"name":"Healer","class":"Paladin"}]}}}]}
                """);
        var guildStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var timelineStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var guildCanceled = false;
        using var handler = new CallbackHandler(async (request, cancellationToken) =>
        {
            if (request.RequestUri!.AbsolutePath == "/oauth/token")
                return """{"access_token":"token","expires_in":3600}""";
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            if (!document.RootElement.GetProperty("query").GetString()!.Contains("CompPlayerGuilds", StringComparison.Ordinal))
                return roster.ToJsonString();
            guildStarted.TrySetResult();
            try { await release.Task.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                guildCanceled = true;
                throw;
            }
            return """{"data":{"characterData":{"c123":{"guilds":[{"name":"Raid Guild"}]}}}}""";
        });
        var timeline = new RecordingTimeline
        {
            Completion = release.Task,
            OnStart = () => timelineStarted.TrySetResult()
        };
        var service = new CompRankingService(fixture.CreateClient(handler), timeline,
            NullLogger<CompRankingService>.Instance, new WarcraftReferenceData(WorldDataService.Instance));
        var endpointHandler = new GetDetailsHandler(service, NullLogger<GetDetailsHandler>.Instance);
        using var cancellation = new CancellationTokenSource();
        var task = endpointHandler.Handle(new GetDetailsRequest("ReportA", 1, "anniversary"), cancellation.Token);
        try
        {
            await Task.WhenAll(guildStarted.Task, timelineStarted.Task).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(task.IsCompleted);
            if (cancel)
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.True(guildCanceled);
                Assert.Equal(cancellation.Token, timeline.Cancellation);
                Assert.True(timeline.Cancellation.IsCancellationRequested);
            }
        }
        finally
        {
            release.TrySetResult();
        }
        if (cancel) return;
        var report = Assert.IsType<RankingReport>(HttpResultAssertions.Read(await task).Value);
        var player = Assert.Single(report.Players);
        Assert.Equal("Raid Guild", player.GuildName);
        Assert.NotNull(player.Timeline);
    }

    [Fact]
    public async Task DetailLoadsOnlyRequestedFightWithCompositionAndObservedPhases()
    {
        using var handler = new QueueHandler(Roster(7));
        var report = await Service(handler).LoadDetailsAsync("ReportA", 7, "anniversary", false, default);
        Assert.Equal(1, handler.GraphQlRequests);
        Assert.Equal(7, Assert.Single(report.Fights).FightId);
        Assert.Equal(1, report.Fights[0].Composition!.Roles["heal"]);
        Assert.Equal(1, report.Fights[0].Composition!.Specs["holy-paladin"]);
        Assert.NotNull(report.Fights[0].Phases);
        Assert.Equal("Healer", Assert.Single(report.Players).Name);
        Assert.True(Assert.Single(report.Players).Timeline!.IsComplete);
        Assert.Equal(7, handler.Requests[0].GetProperty("variables").GetProperty("fightIds")[0].GetInt32());
    }

    [Theory]
    [InlineData(101194, "Zul'jin")]
    [InlineData(1194, "Daakara")]
    public async Task AnniversaryZuljinNameIsCorrectedInSummariesAndDetailsOnly(int encounterId, string expected)
    {
        using var handler = new QueueHandler(
            Rankings(1).Replace("Illidan", "Daakara"),
            Roster(1).Replace("Illidan", "Daakara").Replace("50609", encounterId.ToString()));
        var service = Service(handler);
        var rankings = await service.LoadAsync(Search() with { EncounterId = encounterId }, default);
        Assert.Equal(expected, Assert.Single(Assert.Single(rankings!.Reports).Fights).Name);
        var report = await service.LoadDetailsAsync("ReportA", 1, "anniversary", false, default);
        Assert.Equal(expected, Assert.Single(report.Fights).Name);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(9, null)]
    public async Task SummariesIncludeNormalizedGuildFactionWithoutExtraCalls(int faction, int? expected)
    {
        var payload = System.Text.Json.Nodes.JsonNode.Parse(Rankings(1))!;
        payload["data"]!["worldData"]!["encounter"]!["fightRankings"]!["rankings"]![0]!["guild"] =
            JsonSerializer.SerializeToNode(new { name = "Raid Guild", faction });
        using var handler = new QueueHandler(payload.ToJsonString());
        var result = await Service(handler).LoadAsync(Search(), default);
        Assert.Equal(expected, Assert.Single(result!.Reports).FactionId);
        Assert.Equal(1, handler.GraphQlRequests);
    }

    [Fact]
    public async Task DetailsIncludeParseRegionAndUnambiguousCharacterGuild()
    {
        var roster = System.Text.Json.Nodes.JsonNode.Parse(Roster(7))!;
        var metadata = roster["data"]!["reportData"]!["report"]!;
        metadata["healingRankings"] = System.Text.Json.Nodes.JsonNode.Parse("""
            {"data":[{"fightID":99,"speed":{"rank":999}},{"fightID":7,
            "speed":{"rank":3,"best":2,"totalParses":1175,"rankPercent":99},
            "execution":{"rank":2,"best":1,"totalParses":1175,"rankPercent":99.5},
            "guild":{"name":"Raid Guild","faction":1,"server":{"name":"Thunderstrike","region":"EU"}},
            "roles":{"healers":{"characters":[
              {"id":123,"name":"Healer","class":"Paladin","rankPercent":94,
               "server":{"name":"Spineshatter","region":"EU"}}
            ]}}}]}
            """);
        using var handler = new QueueHandler(roster.ToJsonString(),
            """{"data":{"characterData":{"c123":{"guilds":[{"name":"Raid Guild"}]}}}}""");
        var service = Service(handler);
        var report = await service.LoadDetailsAsync("ReportA", 7, "anniversary", false, default);
        var player = Assert.Single(report.Players);
        Assert.Equal(94, player.ParsePercentile);
        Assert.Equal("EU", player.ServerRegion);
        Assert.Equal("Raid Guild", player.GuildName);
        var fight = Assert.Single(report.Fights);
        Assert.Equal(new RaidFightRanking(3, 2, 1175, 99), fight.Speed);
        Assert.Equal("Raid Guild", report.GuildName);
        Assert.Equal(2, report.FactionId);
        Assert.Equal("Thunderstrike", report.ServerName);
        Assert.Equal("EU", report.ServerRegion);
        Assert.Equal(new RaidFightRanking(2, 1, 1175, 99.5), fight.Execution);
        var json = JsonSerializer.SerializeToElement(report, JsonSerializerOptions.Web);
        Assert.Equal(1175, json.GetProperty("fights")[0].GetProperty("speed").GetProperty("totalParses").GetInt32());
        Assert.Equal(99.5, json.GetProperty("fights")[0].GetProperty("execution").GetProperty("rankPercent").GetDouble());
        Assert.Equal(2, handler.GraphQlRequests);
        await service.LoadDetailsAsync("ReportA", 7, "anniversary", false, default);
        Assert.Equal(2, handler.GraphQlRequests);
    }

    [Fact]
    public async Task SummaryAndDetailsUseOnlyGraphQlCacheAndExplicitRefreshBypassesBoth()
    {
        using var handler = new QueueHandler(Rankings(1), Roster(1), Rankings(1), Roster(1));
        var service = Service(handler);
        for (var i = 0; i < 2; i++)
        {
            await service.LoadAsync(Search(), default);
            await service.LoadDetailsAsync("ReportA", 1, "anniversary", false, default);
        }
        Assert.Equal(2, handler.GraphQlRequests);
        await service.LoadAsync(Search() with { Refresh = true }, default);
        await service.LoadDetailsAsync("ReportA", 1, "anniversary", true, default);
        Assert.Equal(4, handler.GraphQlRequests);
    }

    [Theory]
    [InlineData("", 1, 1, 0, 0, null)]
    [InlineData("retail", 0, 1, 0, 0, null)]
    [InlineData("retail", 1, 0, 0, 0, null)]
    [InlineData("retail", 1, 1, 300, 200, null)]
    [InlineData("retail", 1, 1, 0, 0, "bad.eq.0")]
    public async Task InvalidSearchDoesNotInvokeUpstream(string edition, int encounter, int page, int min, int max, string? role)
    {
        using var handler = new QueueHandler();
        var result = await Handlers(handler).Ranking.Handle(new GetCompRankingRequest("boss", edition, encounter, Page: page, MinSeconds: min, MaxSeconds: max, Roles: role is null ? null : [role]));
        Assert.Equal(400, HttpResultAssertions.Read(result).StatusCode);
        Assert.Equal(0, handler.GraphQlRequests);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RateLimitsPropagateWithoutRequestingOtherKills(bool details)
    {
        using var handler = new QueueHandler { GraphQlFailure = new HttpRequestException("limited", null, HttpStatusCode.TooManyRequests) };
        var handlers = Handlers(handler);
        var response = details ? await handlers.Details.Handle(new GetDetailsRequest("ReportA", 1, "anniversary")) :
            await handlers.Ranking.Handle(new GetCompRankingRequest("boss", "anniversary", 50609));
        Assert.Equal(429, HttpResultAssertions.Read(response).StatusCode);
        Assert.Equal(1, handler.GraphQlRequests);
    }

    [Fact]
    public async Task DetailCancellationIsNotConvertedToSuccessfulResponse()
    {
        using var handler = new QueueHandler();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Handlers(handler).Details.Handle(new GetDetailsRequest("ReportA", 1, "anniversary"), cts.Token));
    }

    [Theory]
    [InlineData("Report\rforged")]
    [InlineData("Report\nforged")]
    [InlineData("Report\r\nforged")]
    [InlineData("Report\u0085forged")]
    [InlineData("Report\u2028forged")]
    [InlineData("Report\u2029forged")]
    public async Task DetailRejectsLogForgingCodesWithoutCallingUpstream(string code)
    {
        using var handler = new QueueHandler();
        var response = await Handlers(handler).Details.Handle(new GetDetailsRequest(code, 1, "anniversary"));
        Assert.Equal(400, HttpResultAssertions.Read(response).StatusCode);
        Assert.Equal(0, handler.GraphQlRequests);
        Assert.Equal(0, handler.TokenRequests);
    }

    [Theory]
    [InlineData("This report is private.", 403)]
    [InlineData("This report has been archived. Subscribing users can access the report content via the /user API endpoint.", 403)]
    [InlineData("Invalid GraphQL field", 502)]
    public async Task DetailFailuresRemainOnTheirRowAsErrors(string message, int status)
    {
        using var handler = new QueueHandler(JsonSerializer.Serialize(new
        {
            errors = new[] { new { message, path = new[] { "reportData", "report" } } }
        }));
        var response = await Handlers(handler).Details.Handle(new GetDetailsRequest("ReportA", 1, "anniversary"));
        Assert.Equal(status, HttpResultAssertions.Read(response).StatusCode);
        Assert.Equal(1, handler.GraphQlRequests);
    }

    [Fact]
    public async Task MissingFightIsNotAnEmptyComposition()
    {
        using var handler = new QueueHandler(Roster(2));
        Assert.Equal(404, HttpResultAssertions.Read(await Handlers(handler).Details.Handle(new GetDetailsRequest("ReportA", 1, "anniversary"))).StatusCode);
    }

    [Fact]
    public void ParsePercentilesUseRoleMetricAndDoNotInventMissingValues()
    {
        using var metadata = JsonDocument.Parse("""
            {"damageRankings":{"data":[{"fightID":7,"roles":{
              "tanks":{"characters":[{"name":"Tank","class":"Paladin","rankPercent":93,"amount":2116}]},
              "healers":{"characters":[{"name":"Healer","class":"Paladin","rankPercent":1}]},
              "dps":{"characters":[{"name":"Damage","class":"Paladin","rankPercent":0}]}
            }}]},"healingRankings":{"data":[{"fightID":7,"roles":{
              "healers":{"characters":[{"name":"Healer","class":"Paladin","rankPercent":88.5}]}
            }}]}}
            """);
        var players = new List<RankingPlayer>
        {
            new() { Name = "Tank", ClassId = 2, SpecSlug = "protection-paladin", RoleCode = "tank" },
            new() { Name = "Healer", ClassId = 2, SpecSlug = "holy-paladin", RoleCode = "heal" },
            new() { Name = "Damage", ClassId = 2, SpecSlug = "retribution-paladin", RoleCode = "mdps" },
            new() { Name = "Missing", ClassId = 2, SpecSlug = "holy-paladin", RoleCode = "heal" }
        };
        Service(new QueueHandler()).ApplyParsePercentiles(players, metadata.RootElement, 7);
        Assert.Equal(93, players[0].ParsePercentile);
        Assert.Equal(2116, players[0].Performance);
        Assert.Equal(88.5, players[1].ParsePercentile);
        Assert.Equal(0, players[2].ParsePercentile);
        Assert.Null(players[3].ParsePercentile);
    }

    [Fact]
    public void UnknownEditionSpecsKeepTheirRosterRole()
    {
        using var json = JsonDocument.Parse("""{"data":{"playerDetails":{"healers":[{"id":7,"name":"Druid","type":"Druid","specs":[{"spec":"Dreamstate"}]}]}}}""");
        var player = Assert.Single(Service(new QueueHandler()).ParseRoster(json.RootElement));
        Assert.Equal("dreamstate-druid", player.SpecSlug);
        Assert.Equal("heal", player.RoleCode);
    }

    private CompRankingService Service(QueueHandler handler) =>
        new(fixture.CreateClient(handler), new RecordingTimeline(), NullLogger<CompRankingService>.Instance, new WarcraftReferenceData(WorldDataService.Instance));
    private (GetCompRankingHandler Ranking, GetDetailsHandler Details) Handlers(QueueHandler handler)
    {
        var service = Service(handler);
        return (new(service, NullLogger<GetCompRankingHandler>.Instance),
            new(service, NullLogger<GetDetailsHandler>.Instance));
    }
    private static CompRankingSearch Search() => new("illidan", "anniversary", 50609, 3, 1, 0, 0, [], [], false);
    private static string Rankings(params int[] ids) => JsonSerializer.Serialize(new
    {
        data = new
        {
            worldData = new
            {
                encounter = new
                {
                    name = "Illidan",
                    fightRankings = new
                    {
                        hasMorePages = true,
                        rankings = ids.Select(id => new
                        {
                            report = new { code = $"Report{id}", fightID = id },
                            duration = 200000 + id,
                            deaths = 0,
                            damageTaken = id * 100,
                            tanks = 2,
                            healers = 4,
                            melee = 9,
                            ranged = 10,
                            startTime = 1793030400000L
                        }).ToArray()
                    }
                }
            }
        }
    });
    private static string Roster(int id) => JsonSerializer.Serialize(new
    {
        data = new
        {
            reportData = new
            {
                report = new
                {
                    title = "Guild kill",
                    table = new
                    {
                        data = new
                        {
                            playerDetails = new
                            {
                                healers = new[] {
                new { id = 7, name = "Healer", type = "Paladin", specs = new[] { new { spec = "Holy" } } }
            }
                            }
                        }
                    },
                    fights = new[] { new { id, name = "Illidan", encounterID = 50609, startTime = 1000, endTime = 201000,
                phaseTransitions = Array.Empty<object>() } }
                }
            }
        }
    });
    private sealed class RecordingTimeline : IReportTimelineService
    {
        public Task Completion { get; init; } = Task.CompletedTask;
        public Action? OnStart { get; init; }
        public CancellationToken Cancellation { get; private set; }

        public async Task PopulateRosterTimelinesAsync(RankingReport report, string edition, bool refresh, CancellationToken cancellationToken)
        {
            Cancellation = cancellationToken;
            OnStart?.Invoke();
            await Completion.WaitAsync(cancellationToken);
            foreach (var player in report.Players)
                player.Timeline = new ReportTimeline(report.ReportId, report.Fights[0].FightId,
                    player.PlayerId, player.Name, report.Duration, true, [], []);
        }
        public Task<BossTimeline?> LoadBossAsync(string reportCode, int fightId, string edition, bool cacheOnly, CancellationToken cancellationToken)
        {
            Assert.True(cacheOnly);
            return Task.FromResult<BossTimeline?>(null);
        }
        public Task PopulateFightPhasesAsync(IReadOnlyList<RankingReport> reports, string edition, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<ReportTimeline> LoadAsync(string reportCode, int fightId, int? sourceId, string? playerName, string edition, CancellationToken cancellationToken, int? specId = null) => throw new NotSupportedException();
        public Task<ReportTimeline?> TryLoadCachedAsync(string reportCode, int fightId, int? sourceId, string? playerName, string edition, CancellationToken cancellationToken, int? specId = null) => Task.FromResult<ReportTimeline?>(null);
    }
}
