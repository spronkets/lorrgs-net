using System.Net;
using System.Text.Json;
using Lorrgs.Api.Features.Rankings.GetSpecRanking;
using Lorrgs.Api.Features.Rankings.GetSpecRankingInfo;
using Lorrgs.Api.Features.Rankings.MarkSpecRankingDirty;
using Lorrgs.Api.Features.WorldData;
using Lorrgs.Tests.TestSupport;
using Lorrgs.WarcraftLogs;
using Lorrgs.WarcraftLogs.Models;
using Lorrgs.WarcraftLogs.Rankings;
using Lorrgs.WarcraftLogs.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lorrgs.Api.Tests;

public sealed class RankingsHandlerTests : IDisposable
{
    private readonly TestClientFixture fixture = new();
    public void Dispose() => fixture.Dispose();

    [Theory]
    [InlineData("edition")]
    [InlineData("zoneId")]
    [InlineData("className")]
    [InlineData("specName")]
    [InlineData("difficulty")]
    [InlineData("metric-empty")]
    [InlineData("metric-invalid")]
    [InlineData("page")]
    [InlineData("partition")]
    [InlineData("bracket")]
    [InlineData("size")]
    [InlineData("hardModeLevel")]
    [InlineData("externalBuffs")]
    public async Task RankingEndpointsRejectInvalidInputsBeforeCallingUpstream(string invalid)
    {
        using var handler = new QueueHandler();
        var client = fixture.CreateClient(handler);
        var handlers = Handlers(client, new RecordingTimelineService());
        var edition = invalid == "edition" ? null : "retail";
        var zoneId = invalid == "zoneId" ? 0 : 38;
        var className = invalid == "className" ? " " : "Paladin";
        var specName = invalid == "specName" ? "" : "Retribution";
        var difficulty = invalid == "difficulty" ? "impossible" : "mythic";
        var metric = invalid == "metric-empty" ? "" : invalid == "metric-invalid" ? "invalid" : "dps";
        int? page = invalid == "page" ? 0 : null;
        int? partition = invalid == "partition" ? -1 : null;
        int? bracket = invalid == "bracket" ? 0 : null;
        int? size = invalid == "size" ? -1 : null;
        var hardMode = invalid == "hardModeLevel" ? "invalid" : null;
        var buffs = invalid == "externalBuffs" ? "invalid" : null;

        var full = await handlers.Ranking.Handle(new GetSpecRankingRequest("retribution-paladin", "eranog", edition, Difficulty: difficulty, Metric: metric, ZoneId: zoneId, ClassName: className, SpecName: specName, Page: page, Partition: partition, Bracket: bracket, Size: size, HardModeLevel: hardMode, ExternalBuffs: buffs));
        var info = await handlers.Info.Handle(new GetSpecRankingInfoRequest("retribution-paladin", "eranog", edition, Difficulty: difficulty, Metric: metric, ZoneId: zoneId, ClassName: className, SpecName: specName, Page: page, Partition: partition, Bracket: bracket, Size: size, HardModeLevel: hardMode, ExternalBuffs: buffs));
        var dirty = await handlers.Dirty.Handle(new MarkSpecRankingDirtyRequest("retribution-paladin", "eranog", edition, Difficulty: difficulty, Metric: metric, ZoneId: zoneId, ClassName: className, SpecName: specName, Page: page, Partition: partition, Bracket: bracket, Size: size, HardModeLevel: hardMode, ExternalBuffs: buffs));

        var fullError = JsonSerializer.Serialize(HttpResultAssertions.Status(400, full).Value);
        var infoError = JsonSerializer.Serialize(HttpResultAssertions.Status(400, info).Value);
        Assert.Equal(fullError, infoError);
        Assert.Equal(fullError, JsonSerializer.Serialize(HttpResultAssertions.Status(400, dirty).Value));
        Assert.Contains(invalid.Split('-')[0], fullError, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, handler.GraphQlRequests);
        Assert.Equal(0, handler.TokenRequests);
    }

    [Theory]
    [InlineData("any", "Any")]
    [InlineData("highest", "Highest")]
    [InlineData("normal-mode", "NormalMode")]
    [InlineData("normal", "NormalMode")]
    [InlineData("level0", "Level0")]
    [InlineData("0", "Level0")]
    [InlineData("level1", "Level1")]
    [InlineData("1", "Level1")]
    [InlineData("level2", "Level2")]
    [InlineData("2", "Level2")]
    [InlineData("level3", "Level3")]
    [InlineData("3", "Level3")]
    [InlineData("level4", "Level4")]
    [InlineData("4", "Level4")]
    public async Task HardModeAliasesAreSentAsCanonicalGraphQlValues(string input, string expected)
    {
        using var handler = new QueueHandler(RankingsPayload());
        var response = await Handlers(fixture.CreateClient(handler), new RecordingTimelineService())
            .Info.Handle(new GetSpecRankingInfoRequest("retribution-paladin", "eranog", "retail", Metric: "dps", ZoneId: 38, ClassName: "Paladin", SpecName: "Retribution", HardModeLevel: input));
        HttpResultAssertions.Status(200, response);
        Assert.Equal(expected, Assert.Single(handler.Requests).GetProperty("variables").GetProperty("hardModeLevel").GetString());
    }

    [Theory]
    [InlineData("any", "Any")]
    [InlineData("require", "Require")]
    [InlineData("with", "Require")]
    [InlineData("include", "Require")]
    [InlineData("with external buffs", "Require")]
    [InlineData("exclude", "Exclude")]
    [InlineData("without", "Exclude")]
    [InlineData("no", "Exclude")]
    [InlineData("without external buffs", "Exclude")]
    public async Task ExternalBuffAliasesAreSentAsCanonicalGraphQlValues(string input, string expected)
    {
        using var handler = new QueueHandler(RankingsPayload());
        var response = await Handlers(fixture.CreateClient(handler), new RecordingTimelineService())
            .Info.Handle(new GetSpecRankingInfoRequest("retribution-paladin", "eranog", "retail", Metric: "hps", ZoneId: 38, ClassName: "Paladin", SpecName: "Holy", ExternalBuffs: input));
        HttpResultAssertions.Status(200, response);
        Assert.Equal(expected, Assert.Single(handler.Requests).GetProperty("variables").GetProperty("externalBuffs").GetString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MarkDirtyAcknowledgesStateWithoutFetchingUpstream(bool dirty)
    {
        using var handler = new QueueHandler();
        var response = await Handlers(fixture.CreateClient(handler), new RecordingTimelineService())
            .Dirty.Handle(new MarkSpecRankingDirtyRequest("retribution-paladin", "eranog", "retail", Metric: " DPS ", ZoneId: 38, ClassName: "Paladin", SpecName: "Retribution", Dirty: dirty));
        var json = JsonSerializer.SerializeToElement(HttpResultAssertions.Status(200, response).Value, JsonSerializerOptions.Web);
        Assert.Equal("dps", json.GetProperty("metric").GetString());
        Assert.Equal("mythic", json.GetProperty("difficulty").GetString());
        Assert.Contains(dirty ? "refresh" : "clean", json.GetProperty("message").GetString());
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(true, 429)]
    [InlineData(false, 500)]
    public async Task RankingEndpointsSurfaceRateLimitsAndUnexpectedUpstreamFailures(bool rateLimited, int status)
    {
        Exception failure = rateLimited
            ? new HttpRequestException("Limited", null, HttpStatusCode.TooManyRequests)
            : new JsonException("Invalid response");
        using var handler = new QueueHandler { GraphQlFailure = failure };
        var handlers = Handlers(fixture.CreateClient(handler), new RecordingTimelineService());
        var full = await handlers.Ranking.Handle(new GetSpecRankingRequest("retribution-paladin", "eranog", "retail", Metric: "dps", ZoneId: 38, ClassName: "Paladin", SpecName: "Retribution"));
        var info = await handlers.Info.Handle(new GetSpecRankingInfoRequest("retribution-paladin", "eranog", "retail", Metric: "dps", ZoneId: 38, ClassName: "Paladin", SpecName: "Retribution"));
        Assert.Equal(status, HttpResultAssertions.Read(full).StatusCode);
        Assert.Equal(status, HttpResultAssertions.Read(info).StatusCode);
        Assert.Equal(2, handler.GraphQlRequests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PercentileEnrichmentMatchesFightThenClassSpecAndClosestOutput(bool nested)
    {
        const string entries = """
            [{"name":"Player","amount":100,"report":{"code":"abc","fightID":7}}]
            """;
        const string ranks = """
            [null,{"fightID":8,"roles":{"dps":{"characters":[{"name":"Player","rankPercent":1}]}}},
             {"fightID":7,"roles":null},{"fightID":7,"roles":{
             "bad":null,"empty":{},"malformed":{"characters":{}},
             "dps":{"characters":[null,{"name":"Other","rankPercent":1},{"name":"Player"},
               {"name":"Player","rankPercent":5,"class":"Mage","spec":"Fire","amount":100},
               {"name":"Player","rankPercent":80,"class":"Paladin","spec":"Retribution","amount":150},
               {"name":"Player","rankPercent":99,"class":"Paladin","spec":"Retribution","amount":101},
               {"name":"Player","rankPercent":60,"class":"Paladin","spec":"Retribution","amount":200}]}}}]
            """;
        var rankings = nested ? JsonSerializer.SerializeToElement(new { data = JsonSerializer.Deserialize<JsonElement>(ranks) })
            : JsonSerializer.Deserialize<JsonElement>(ranks);
        var enrichment = JsonSerializer.Serialize(new
        {
            data = new { reportData = new { r0 = new { title = "Enriched", rankings } } }
        });
        using var handler = new QueueHandler(RankingsPayload(entries), enrichment);
        var response = await Handlers(fixture.CreateClient(handler), new RecordingTimelineService())
            .Ranking.Handle(new GetSpecRankingRequest("retribution-paladin", "eranog", "retail", Metric: "dps", ZoneId: 38, ClassName: "Paladin", SpecName: "Retribution"));
        var report = Assert.Single(Assert.IsType<SpecRankingData>(HttpResultAssertions.Status(200, response).Value).Reports);
        Assert.Equal("Enriched", report.Title);
        Assert.Equal(99, report.Percentile);
        Assert.Equal(99, Assert.Single(report.Fights).Percentile);
        Assert.Equal(2, handler.GraphQlRequests);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"data\":[]}")]
    [InlineData("[{\"fightID\":7,\"roles\":{\"dps\":{\"characters\":[{\"name\":\"Player\",\"rankPercent\":\"bad\"}]}}}]")]
    public async Task UnresolvablePercentilesRemainUnknownRatherThanInventingZero(string ranks)
    {
        const string entries = """[{"name":"Player","report":{"code":"abc","fightID":7}}]""";
        var enrichment = JsonSerializer.Serialize(new
        {
            data = new { reportData = new { r0 = new { rankings = JsonSerializer.Deserialize<JsonElement>(ranks) } } }
        });
        using var handler = new QueueHandler(RankingsPayload(entries), enrichment);
        var response = await Handlers(fixture.CreateClient(handler), new RecordingTimelineService())
            .Ranking.Handle(new GetSpecRankingRequest("retribution-paladin", "eranog", "retail", Metric: "dps", ZoneId: 38, ClassName: "Paladin", SpecName: "Retribution"));
        var report = Assert.Single(Assert.IsType<SpecRankingData>(HttpResultAssertions.Status(200, response).Value).Reports);
        Assert.Null(report.Percentile);
        Assert.Null(Assert.Single(report.Fights).Percentile);
    }

    [Theory]
    [InlineData("{\"faction\":{\"id\":2},\"guilds\":[{\"name\":\"Guild\",\"faction\":{\"id\":1}}]}", 2, "Guild")]
    [InlineData("{\"guilds\":[{\"name\":\"Guild\",\"faction\":\"1\"}]}", 1, "Guild")]
    [InlineData("{\"faction\":2,\"guilds\":[]}", 2, null)]
    [InlineData("{\"faction\":{},\"guilds\":[{},{}]}", null, null)]
    [InlineData("{\"faction\":\"bad\",\"guilds\":[null]}", null, null)]
    [InlineData("null", null, null)]
    public async Task FactionEnrichmentUsesCharacterFactionBeforeSingleGuildFallback(
        string character, int? faction, string? guild)
    {
        const string entries = """
            [{"name":"Player","percentile":90,"regionName":"NA","serverName":"Quel'  Thalas",
              "report":{"code":"abc","fightID":7}}]
            """;
        var enrichment = JsonSerializer.Serialize(new
        {
            data = new { characterData = new { c0 = JsonSerializer.Deserialize<JsonElement>(character) } }
        });
        using var handler = new QueueHandler(RankingsPayload(entries), enrichment);
        var response = await Handlers(fixture.CreateClient(handler), new RecordingTimelineService())
            .Ranking.Handle(new GetSpecRankingRequest("retribution-paladin", "eranog", "retail", Metric: "dps", ZoneId: 38, ClassName: "Paladin", SpecName: "Retribution"));
        var player = Assert.Single(Assert.Single(
            Assert.IsType<SpecRankingData>(HttpResultAssertions.Status(200, response).Value).Reports).Players);
        Assert.Equal(faction, player.FactionId);
        Assert.Equal(guild, player.GuildName);
        Assert.Equal("US", player.ServerRegion);
        Assert.Equal("quel-thalas", player.ServerSlug);
        var query = handler.Requests[1].GetProperty("query").GetString();
        Assert.Contains("serverRegion: \"US\"", query);
        Assert.Contains("serverSlug: \"quel-thalas\"", query);
    }

    [Theory]
    [InlineData("region", "EU", "Realm", "realm")]
    [InlineData("serverRegion", "na", "R\u00e9alm", "realm")]
    public async Task FactionLookupDeduplicatesCharactersAcrossReportsAndNormalizesRealmNames(
        string regionField, string region, string realm, string expectedRealm)
    {
        var entry = new Dictionary<string, object?>
        {
            ["name"] = "Player", ["percentile"] = 90, [regionField] = region,
            ["realmName"] = realm, ["report"] = new { code = "abc", fightID = 7 }
        };
        var entries = JsonSerializer.Serialize(new[] { entry, entry });
        using var handler = new QueueHandler(RankingsPayload(entries),
            """{"data":{"characterData":{"c0":{"faction":{"id":1},"guilds":[{"name":"Guild"}]}}}}""");
        var response = await Handlers(fixture.CreateClient(handler), new RecordingTimelineService())
            .Ranking.Handle(new GetSpecRankingRequest("retribution-paladin", "eranog", "retail", Metric: "dps", ZoneId: 38, ClassName: "Paladin", SpecName: "Retribution"));
        var reports = Assert.IsType<SpecRankingData>(HttpResultAssertions.Status(200, response).Value).Reports;
        Assert.Equal(2, reports.Count);
        Assert.All(reports, report =>
        {
            var player = Assert.Single(report.Players);
            Assert.Equal(1, player.FactionId);
            Assert.Equal("Guild", player.GuildName);
            Assert.Equal(expectedRealm, player.ServerSlug);
        });
        var query = handler.Requests[1].GetProperty("query").GetString();
        Assert.Contains("c0:", query);
        Assert.DoesNotContain("c1:", query);
        Assert.Equal(2, handler.GraphQlRequests);
    }

    [Theory]
    [InlineData("lfr", 2)]
    [InlineData("normal", 3)]
    [InlineData(" HEROIC ", 4)]
    [InlineData("mythic", 5)]
    [InlineData("7", 7)]
    [InlineData("", 5)]
    public async Task GetSpecRankingForwardsNormalizedOptionsAndDifficulty(string difficulty, int expectedDifficulty)
    {
        using var handler = new QueueHandler(RankingsPayload());
        var timelines = new RecordingTimelineService();
        var handlers = Handlers(fixture.CreateClient(handler), timelines);

        var response = await handlers.Ranking.Handle(new GetSpecRankingRequest("retribution-paladin", "eranog", "retail", Difficulty: difficulty, Metric: " DPS ", ZoneId: 38, EncounterId: 2898, ClassName: " Paladin ", SpecName: " Retribution ", Page: 2, Partition: 3, Bracket: 4, ServerRegion: " EU ", ServerSlug: " realm ", Filter: " value ", HardModeLevel: "level-2", ExternalBuffs: "without", IncludeOtherPlayers: false));

        var data = Assert.IsType<SpecRankingData>(HttpResultAssertions.Status(200, response).Value);
        Assert.Equal("dps", data.Metric);
        Assert.Equal(2, data.Page);
        Assert.Equal(0, data.TotalCount);
        Assert.True(data.HasMorePages);
        var variables = Assert.Single(handler.Requests).GetProperty("variables");
        Assert.Equal(expectedDifficulty, variables.GetProperty("difficulty").GetInt32());
        Assert.Equal(2898, variables.GetProperty("encounterId").GetInt32());
        Assert.False(variables.TryGetProperty("zoneId", out _));
        var query = handler.Requests[0].GetProperty("query").GetString();
        Assert.Contains("encounter(id: $encounterId)", query);
        Assert.DoesNotContain("encounters", query);
        Assert.Equal("Paladin", variables.GetProperty("className").GetString());
        Assert.Equal("Retribution", variables.GetProperty("specName").GetString());
        Assert.Equal(2, variables.GetProperty("page").GetInt32());
        Assert.Equal(3, variables.GetProperty("partition").GetInt32());
        Assert.Equal(4, variables.GetProperty("bracket").GetInt32());
        Assert.Equal("EU", variables.GetProperty("serverRegion").GetString());
        Assert.Equal("realm", variables.GetProperty("serverSlug").GetString());
        Assert.Equal("value", variables.GetProperty("filter").GetString());
        Assert.Equal("Level2", variables.GetProperty("hardModeLevel").GetString());
        Assert.Equal("Exclude", variables.GetProperty("externalBuffs").GetString());
        Assert.True(variables.GetProperty("includeCombatantInfo").GetBoolean());
        Assert.False(variables.GetProperty("includeOtherPlayers").GetBoolean());
        Assert.Equal(1, timelines.PhaseLoads);
    }

    [Theory]
    [InlineData("Invalid difficulty setting or size specified.", true)]
    [InlineData(null, true)]
    [InlineData("Invalid difficulty setting or size specified.", false)]
    public async Task RankingRecoversFromRejectedSizeEmptySizeOrRejectedDifficulty(string? error, bool hasSize)
    {
        using var handler = new QueueHandler(RankingsPayload(error: error), RankingsPayload());
        var handlers = Handlers(fixture.CreateClient(handler), new RecordingTimelineService());

        var response = await handlers.Ranking.Handle(new GetSpecRankingRequest("retribution-paladin", "eranog", "retail", Metric: "dps", ZoneId: 38, EncounterId: 2898, ClassName: "Paladin", SpecName: "Retribution", Size: hasSize ? 10 : null));

        HttpResultAssertions.Status(200, response);
        Assert.Equal(2, handler.GraphQlRequests);
        var retry = handler.Requests[1].GetProperty("variables");
        Assert.Equal(JsonValueKind.Null, retry.GetProperty(hasSize ? "size" : "difficulty").ValueKind);
        if (hasSize) Assert.Equal(5, retry.GetProperty("difficulty").GetInt32());
    }

    [Theory]
    [InlineData("{\"data\":{}}")]
    [InlineData("{\"data\":{\"worldData\":{\"encounter\":null}}}")]
    public async Task MissingEncounterDataProducesEmptyRankingWithoutMoreUpstreamCalls(string payload)
    {
        using var handler = new QueueHandler(payload);
        var handlers = Handlers(fixture.CreateClient(handler), new RecordingTimelineService());
        var response = await handlers.Ranking.Handle(new GetSpecRankingRequest("retribution-paladin", "eranog", "retail", Metric: "dps", ZoneId: 38, ClassName: "Paladin", SpecName: "Retribution"));
        var ranking = Assert.IsType<SpecRankingData>(HttpResultAssertions.Status(200, response).Value);
        Assert.Empty(ranking.Reports);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnmappedBossDiscoversOnlyEncounterMetadataBeforeQueryingSelectedRankings(bool found)
    {
        var metadata = JsonSerializer.Serialize(new
        {
            data = new
            {
                worldData = new
                {
                    zone = new
                    {
                        encounters = new[]
                        {
                            new { id = 2898, name = found ? "New Encounter" : "Unrelated" },
                            new { id = 9999, name = "Another Encounter" }
                        }
                    }
                }
            }
        });
        using var handler = new QueueHandler(metadata, RankingsPayload());
        var handlers = Handlers(fixture.CreateClient(handler), new RecordingTimelineService());

        var result = await handlers.Ranking.Handle(new GetSpecRankingRequest("retribution-paladin", "new-encounter", "retail", Metric: "dps", ZoneId: 38, ClassName: "Paladin", SpecName: "Retribution"));

        HttpResultAssertions.Status(200, result);
        Assert.Equal(found ? 2 : 1, handler.GraphQlRequests);
        var discoveryQuery = handler.Requests[0].GetProperty("query").GetString();
        Assert.Contains("encounters", discoveryQuery);
        Assert.DoesNotContain("characterRankings", discoveryQuery);
        if (found)
        {
            Assert.Equal(2898, handler.Requests[1].GetProperty("variables").GetProperty("encounterId").GetInt32());
            Assert.DoesNotContain("encounters", handler.Requests[1].GetProperty("query").GetString());
        }
    }

    [Theory]
    [InlineData("0", 1)]
    [InlineData("1", 2)]
    [InlineData("\"Alliance\"", 1)]
    [InlineData("\"Horde\"", 2)]
    public async Task RankingFactionIsNormalizedWithoutGuildOrRealmLookup(string faction, int expected)
    {
        var entries = $$"""
            [{"name":"Player","percentile":99,"faction":{{faction}},"guild":null,
              "server":{"region":"KR"},
              "report":{"code":"abc","fightID":7},
              "allCharacters":[{"name":"Peer","class":"Warrior","spec":"Arms","faction":{{faction}}}]}]
            """;
        using var handler = new QueueHandler(RankingsPayload(entries));
        var response = await Handlers(fixture.CreateClient(handler), new RecordingTimelineService())
            .Ranking.Handle(new GetSpecRankingRequest("retribution-paladin", "eranog", "anniversary",
                Metric: "dps", ZoneId: 38, EncounterId: 2898, ClassName: "Paladin", SpecName: "Retribution"));

        var report = Assert.Single(Assert.IsType<SpecRankingData>(
            HttpResultAssertions.Status(200, response).Value).Reports);
        Assert.All(report.Players, player => Assert.Equal(expected, player.FactionId));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetSpecRankingMapsVisibleReportsAndBundlesCachedTimelineWithObservedPhases()
    {
        const string entries = """
            [{"hidden":true,"report":{"code":"hidden"}},
             {"name":"Missing report"},
             {"report":{"code":" "}},
             {"name":"Player","amount":123.4,"percentile":99,"duration":60000,"startTime":1700000000000,
              "guild":{"name":"Guild","faction":1},"report":{"code":"abc","fightID":7,"startTime":1700000000},
              "allCharacters":[{"name":" "},{"name":"Player","class":"Paladin","spec":"Retribution"},
                {"name":"Peer","class":"Warrior","spec":"Arms"}]}]
            """;
        using var handler = new QueueHandler(RankingsPayload(entries));
        var timelines = new RecordingTimelineService
        {
            Player = new("abc", 7, 5, "Player", 60000, true, [], []),
            Boss = new("abc", 7, "Eranog", 60000, true, [], [], [new(2, 30000)])
        };
        var handlers = Handlers(fixture.CreateClient(handler), timelines);

        var response = await handlers.Ranking.Handle(new GetSpecRankingRequest("retribution-paladin", "eranog", "retail", Metric: "dps", ZoneId: 38, EncounterId: 2898, ClassName: "Paladin", SpecName: "Retribution"));

        var ranking = Assert.IsType<SpecRankingData>(HttpResultAssertions.Status(200, response).Value);
        var report = Assert.Single(ranking.Reports);
        Assert.Equal("abc", report.ReportId);
        Assert.Equal("Report abc", report.Title);
        Assert.Equal(99, report.Percentile);
        Assert.Equal(60000, report.Duration);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000).UtcDateTime, report.StartTime);
        var fight = Assert.Single(report.Fights);
        Assert.True(fight.IsKill);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000).UtcDateTime, fight.StartTime);
        Assert.Equal(timelines.Boss.Phases, fight.Phases);
        Assert.Same(timelines.Player, report.Timeline);
        Assert.Same(timelines.Boss, report.BossTimeline);
        Assert.Equal(2, report.Players.Count);
        Assert.Equal("Player", report.Players[0].Name);
        Assert.Equal(70, report.Players[0].SpecId);
        Assert.Equal(2, report.Players[0].ClassId);
        Assert.Equal("Guild", report.Players[0].GuildName);
        Assert.Equal(2, report.Players[0].FactionId);
        Assert.Equal(123.4, report.Players[0].Performance);
        Assert.Equal("arms-warrior", report.Players[1].SpecSlug);
        Assert.Equal(("abc", 7, (int?)null, "Player", "retail", (int?)70), timelines.PlayerRequest);
        Assert.Equal(1, timelines.BossLoads);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetSpecRankingInfoOmitsReportDetailsAndDoesNotLoadTimelines()
    {
        const string entries = """
            [{"name":"Player","serverRegion":"EU","serverSlug":"realm","report":{"code":"abc","fightID":1}}]
            """;
        using var handler = new QueueHandler(RankingsPayload(entries));
        var timelines = new RecordingTimelineService();
        var handlers = Handlers(fixture.CreateClient(handler), timelines);
        var response = await handlers.Info.Handle(new GetSpecRankingInfoRequest("retribution-paladin", "eranog", "retail", Metric: "dps", ZoneId: 38, ClassName: "Paladin", SpecName: "Retribution", DifficultyId: 4));
        var ranking = Assert.IsType<SpecRankingData>(HttpResultAssertions.Status(200, response).Value);
        Assert.Empty(ranking.Reports);
        Assert.Equal(0, timelines.PhaseLoads);
        Assert.Equal(0, timelines.BossLoads);
        Assert.Single(handler.Requests);
        Assert.Equal(4, handler.Requests[0].GetProperty("variables").GetProperty("difficulty").GetInt32());
    }

    [Fact]
    public async Task CachedPrivateReportIsRemovedWithoutFailingRankingResponse()
    {
        const string entries = """[{"name":"Player","percentile":90,"report":{"code":"abc","fightID":1}}]""";
        using var handler = new QueueHandler(RankingsPayload(entries));
        var timelines = new RecordingTimelineService { CacheFailure = new WarcraftLogsReportPrivateException("private") };
        var handlers = Handlers(fixture.CreateClient(handler), timelines);
        var response = await handlers.Ranking.Handle(new GetSpecRankingRequest("retribution-paladin", "eranog", "retail", Metric: "dps", ZoneId: 38, ClassName: "Paladin", SpecName: "Retribution"));
        Assert.Empty(Assert.IsType<SpecRankingData>(HttpResultAssertions.Status(200, response).Value).Reports);
        Assert.Equal(0, timelines.BossLoads);
    }

    [Fact]
    public async Task PercentileEnrichmentKeepsHealthyPartialDataAndDropsOnlyMissingReports()
    {
        const string partial = """
            {"data":{"reportData":{"r0":null,"r1":{"title":"Healthy title",
            "fights":[{"id":2,"startTime":0,"endTime":10000,"phaseTransitions":[]}],"rankings":[]}}},
            "errors":[{"message":"This report does not exist.","path":["reportData","r0"]}]}
            """;
        using var handler = new QueueHandler(partial);
        var reports = new List<RankingReport> { Report("aEnrichmentMissing", 1), Report("bEnrichmentHealthy", 2) };

        await Enrich(fixture.CreateClient(handler), reports);

        Assert.Equal("Healthy title", Assert.Single(reports).Title);
        Assert.Equal(1, handler.GraphQlRequests);
    }

    [Fact]
    public async Task PercentileEnrichmentRetriesPathlessFailuresPerReportAndKeepsHealthyReports()
    {
        const string pathless = """{"data":null,"errors":[{"message":"You do not have permission to view this report."}]}""";
        const string healthy = """
            {"data":{"reportData":{"r1":{"title":"Recovered title",
            "fights":[{"id":4,"startTime":0,"endTime":10000,"phaseTransitions":[]}],"rankings":[]}}}}
            """;
        using var handler = new QueueHandler(pathless, pathless, healthy);
        var reports = new List<RankingReport> { Report("enrichmentPrivatePathless", 3), Report("enrichmentRecovery", 4) };

        await Enrich(fixture.CreateClient(handler), reports);

        Assert.Equal("Recovered title", Assert.Single(reports).Title);
        Assert.Equal(3, handler.GraphQlRequests);
    }

    [Fact]
    public async Task PercentileBatchesStartTogetherAndRemoveOnlyUnavailableReports()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = 0;
        using var handler = new CallbackHandler(async (request, cancellationToken) =>
        {
            if (request.RequestUri!.AbsolutePath == "/oauth/token")
                return """{"access_token":"token","expires_in":3600}""";
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var query = document.RootElement.GetProperty("query").GetString()!;
            if (Interlocked.Increment(ref requests) == 3) started.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            if (query.Contains("\"parallel20\"", StringComparison.Ordinal))
                return """{"data":{"reportData":{"r0":null}},"errors":[{"message":"This report is private","path":["reportData","r0"]}]}""";
            var data = Enumerable.Range(0, 10).ToDictionary(index => $"r{index}",
                _ => new { title = "Enriched", rankings = Array.Empty<object>() });
            return JsonSerializer.Serialize(new { data = new { reportData = data } });
        });
        var reports = Enumerable.Range(0, 21).Select(index => Report($"parallel{index:D2}", 1)).ToList();
        var task = Enrich(fixture.CreateClient(handler), reports);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(task.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
            await task;
        }
        Assert.Equal(3, requests);
        Assert.Equal(20, reports.Count);
        Assert.All(reports, report => Assert.Equal("Enriched", report.Title));
    }

    [Fact]
    public async Task PercentileEnrichmentSurfacesUnknownErrorsWithoutPerReportRetries()
    {
        using var handler = new QueueHandler("""{"errors":[{"message":"Invalid query argument."}]}""");

        await Assert.ThrowsAsync<WarcraftLogsGraphQlException>(() => Enrich(fixture.CreateClient(handler),
            [Report("enrichmentBrokenQuery", 5)]));

        Assert.Equal(1, handler.GraphQlRequests);
    }

    [Fact]
    public async Task PercentileEnrichmentQueriesEachReportOnceAndCombinesOnlyRequestedFightsWithPhases()
    {
        const string enrichment = """
            {"data":{"reportData":{"r0":{"title":"Shared report",
            "fights":[
              {"id":7,"startTime":0,"endTime":10000,"phaseTransitions":[]},
              {"id":8,"startTime":0,"endTime":10000,"phaseTransitions":[]}],
            "rankings":[
              {"fightID":7,"roles":{"dps":{"characters":[{"name":"Player","rankPercent":99}]}}},
              {"fightID":8,"roles":{"dps":{"characters":[{"name":"Player","rankPercent":90}]}}}
            ]}}}}
            """;
        using var handler = new QueueHandler(enrichment);
        var client = fixture.CreateClient(handler);
        var reports = Enumerable.Range(0, 25).Select(index =>
        {
            var report = Report("shared", index % 2 == 0 ? 7 : 8);
            report.Players.Add(new RankingPlayer { Name = "Player", SpecSlug = "retribution-paladin" });
            return report;
        }).ToList();

        await Enrich(client, reports);
        await new ReportTimelineService(client, NullLogger<ReportTimelineService>.Instance)
            .PopulateFightPhasesAsync(reports, "retail", CancellationToken.None);

        var query = Assert.Single(handler.Requests).GetProperty("query").GetString();
        Assert.Contains("r0: report", query);
        Assert.DoesNotContain("r1:", query);
        Assert.Contains("rankings(fightIDs: [7, 8], playerMetric: $playerMetric)", query);
        Assert.Contains("fights(fightIDs: [7, 8])", query);
        Assert.Equal(25, reports.Count);
        Assert.All(reports, report =>
        {
            var fight = Assert.Single(report.Fights);
            Assert.Equal(fight.FightId == 7 ? 99 : 90, report.Percentile);
            Assert.Equal(report.Percentile, fight.Percentile);
            Assert.NotNull(fight.Phases);
        });
    }

    [Fact]
    public async Task PercentileBatchesReuseFileCacheWhenRankingOrderChanges()
    {
        const string enrichment = """
            {"data":{"reportData":{
              "r0":{"rankings":[],"fights":[{"id":7}]},"r1":{"rankings":[],"fights":[{"id":8}]}
            }}}
            """;
        using var handler = new QueueHandler(enrichment);
        await Enrich(fixture.CreateClient(handler), [Report("b", 8), Report("a", 7)]);
        await Enrich(fixture.CreateClient(handler), [Report("a", 7), Report("b", 8)]);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task PercentileEnrichmentBoundsBatchesByUniqueReportCount()
    {
        static string Payload(int count) => JsonSerializer.Serialize(new
        {
            data = new
            {
                reportData = Enumerable.Range(0, count).ToDictionary(index => $"r{index}",
                    _ => new { rankings = Array.Empty<object>(), fights = Array.Empty<object>() })
            }
        });
        using var handler = new QueueHandler(Payload(10), Payload(10), Payload(1));
        var reports = Enumerable.Range(0, 21).SelectMany(index =>
            new[] { Report($"report{index:D2}", 7), Report($"report{index:D2}", 8) }).ToList();

        await Enrich(fixture.CreateClient(handler), reports);

        Assert.Equal(42, reports.Count);
        Assert.Equal(3, handler.GraphQlRequests);
        Assert.Equal(new[] { 10, 10, 1 }, handler.Requests.Select(request =>
            request.GetProperty("query").GetString()!.Split(": report(").Length - 1));
    }

    [Fact]
    public async Task FactionEnrichmentBoundsBatchesToTwentyUniqueCharacters()
    {
        var entries = JsonSerializer.Serialize(Enumerable.Range(0, 21).Select(index => new
        {
            name = $"Player{index:D2}", percentile = 99, serverRegion = "EU", serverSlug = "realm",
            report = new { code = $"report{index:D2}", fightID = 7 }
        }));
        static string Payload(int count) => JsonSerializer.Serialize(new
        {
            data = new
            {
                characterData = Enumerable.Range(0, count).ToDictionary(index => $"c{index}",
                    _ => new { faction = new { id = 1 }, guilds = Array.Empty<object>() })
            }
        });
        using var handler = new QueueHandler(RankingsPayload(entries), Payload(20), Payload(1));
        var response = await Handlers(fixture.CreateClient(handler), new RecordingTimelineService())
            .Ranking.Handle(new GetSpecRankingRequest("retribution-paladin", "eranog", "retail", Metric: "dps", ZoneId: 38, ClassName: "Paladin", SpecName: "Retribution", IncludeOtherPlayers: false));
        var reports = Assert.IsType<SpecRankingData>(HttpResultAssertions.Status(200, response).Value).Reports;

        Assert.Equal(21, reports.Count);
        Assert.All(reports, report => Assert.Equal(1, Assert.Single(report.Players).FactionId));
        Assert.Equal(3, handler.GraphQlRequests);
        Assert.Equal(new[] { 20, 1 }, handler.Requests.Skip(1).Select(request =>
            request.GetProperty("query").GetString()!.Split(": character(").Length - 1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledRankingRequestsDoNotStartUpstreamQueries(bool infoOnly)
    {
        using var handler = new QueueHandler();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var handlers = Handlers(fixture.CreateClient(handler), new RecordingTimelineService());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => infoOnly
            ? handlers.Info.Handle(new GetSpecRankingInfoRequest("retribution-paladin", "eranog", "retail", Metric: "dps", ZoneId: 38, ClassName: "Paladin", SpecName: "Retribution"), cancellation.Token)
            : handlers.Ranking.Handle(new GetSpecRankingRequest("retribution-paladin", "eranog", "retail", Metric: "dps", ZoneId: 38, ClassName: "Paladin", SpecName: "Retribution"), cancellation.Token));
        Assert.Empty(handler.Requests);
        Assert.Equal(0, handler.TokenRequests);
    }

    [Fact]
    public async Task PercentilePartialAccessFailureRemovesEveryEntryForTheUnavailableReport()
    {
        const string partial = """
            {"data":{"reportData":{"r0":null}},
             "errors":[{"message":"This report is private","path":["reportData","r0"]}]}
            """;
        using var handler = new QueueHandler(partial);
        var reports = new List<RankingReport> { Report("private", 1), Report("private", 2) };
        await Enrich(fixture.CreateClient(handler), reports);
        Assert.Empty(reports);
        Assert.Single(handler.Requests);
    }

    private static Task Enrich(WarcraftLogsClient client, List<RankingReport> reports)
    {
        var data = new RankingsDataService(NullLogger<RankingsDataService>.Instance, client,
            new WarcraftReferenceData(WorldDataService.Instance), new RecordingTimelineService());
        var context = new RankingQueryContext(1, 3470, "Paladin", "Retribution",
            "retribution-paladin", "nekzali-the-soulcoiler", "Nek'zali the Soulcoiler", 2, 70);
        return data.PopulatePlayerPercentilesAsync(reports, context, "dps", "retail", false, CancellationToken.None);
    }

    private static RankingReport Report(string code, int fightId) => new()
    {
        ReportId = code,
        Title = code,
        Fights = [new RankingFight { FightId = fightId, Name = "Encounter" }]
    };

    private static (GetSpecRankingHandler Ranking, GetSpecRankingInfoHandler Info, MarkSpecRankingDirtyHandler Dirty)
        Handlers(WarcraftLogsClient client, IReportTimelineService timelines)
    {
        var data = new RankingsDataService(NullLogger<RankingsDataService>.Instance, client,
            new WarcraftReferenceData(WorldDataService.Instance), timelines);
        return (new(NullLogger<GetSpecRankingHandler>.Instance, data),
            new(NullLogger<GetSpecRankingInfoHandler>.Instance, data),
            new(NullLogger<MarkSpecRankingDirtyHandler>.Instance, data));
    }

    private static string RankingsPayload(string entries = "[]", string? error = null) =>
        JsonSerializer.Serialize(new
        {
            data = new
            {
                worldData = new
                {
                    encounter = new
                    {
                        id = 2898, name = "Eranog",
                        characterRankings = new
                        {
                            page = 2, count = entries == "[]" ? 0 : 101, hasMorePages = true,
                            rankings = JsonSerializer.Deserialize<JsonElement>(entries), error
                        }
                    }
                }
            }
        });

    private sealed class RecordingTimelineService : IReportTimelineService
    {
        public Task PopulateRosterTimelinesAsync(RankingReport report, string edition, bool refresh, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public int PhaseLoads { get; private set; }
        public int BossLoads { get; private set; }
        public ReportTimeline? Player { get; init; }
        public BossTimeline? Boss { get; init; }
        public Exception? CacheFailure { get; init; }
        public (string, int, int?, string?, string, int?)? PlayerRequest { get; private set; }

        public Task<ReportTimeline> LoadAsync(string reportCode, int fightId, int? sourceId, string? playerName,
            string edition, CancellationToken cancellationToken, int? specId = null) =>
            throw new InvalidOperationException("Rankings must not fetch uncached player events.");

        public Task<ReportTimeline?> TryLoadCachedAsync(string reportCode, int fightId, int? sourceId, string? playerName,
            string edition, CancellationToken cancellationToken, int? specId = null)
        {
            PlayerRequest = (reportCode, fightId, sourceId, playerName, edition, specId);
            return CacheFailure is null ? Task.FromResult(Player) : Task.FromException<ReportTimeline?>(CacheFailure);
        }

        public Task<BossTimeline?> LoadBossAsync(string reportCode, int fightId, string edition, bool cacheOnly,
            CancellationToken cancellationToken)
        {
            Assert.True(cacheOnly);
            BossLoads++;
            return Task.FromResult(Boss);
        }

        public Task PopulateFightPhasesAsync(IReadOnlyList<RankingReport> reports, string edition,
            CancellationToken cancellationToken)
        {
            PhaseLoads++;
            return Task.CompletedTask;
        }
    }
}
