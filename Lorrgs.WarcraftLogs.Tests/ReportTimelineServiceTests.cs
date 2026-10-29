using System.Text.Json;
using Lorrgs.Tests.TestSupport;
using Lorrgs.WarcraftLogs;
using Lorrgs.WarcraftLogs.Configuration;
using Lorrgs.WarcraftLogs.Models;
using Lorrgs.WarcraftLogs.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lorrgs.WarcraftLogs.Tests;

public sealed class ReportTimelineServiceTests : IDisposable
{
    private readonly TestClientFixture fixture = new();
    public void Dispose() => fixture.Dispose();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NormalizeEventsFiltersActorAndFightAndPreservesMeasuredBuffIntervals(bool complete)
    {
        var events = ParseEvents("""
            [{"type":"cast","timestamp":999,"sourceID":7,"abilityGameID":1},
             {"type":"cast","timestamp":1500,"sourceID":8,"abilityGameID":1},
             {"type":"cast","timestamp":2000,"sourceID":7,"abilityGameID":1},
             {"type":"cast","timestamp":2100,"sourceID":7,"abilityGameID":0},
             {"type":"removebuff","timestamp":2500,"targetID":7,"abilityGameID":2},
             {"type":"applybuff","timestamp":3000,"targetID":7,"abilityGameID":2},
             {"type":"applybuff","timestamp":3500,"targetID":7,"abilityGameID":2},
             {"type":"refreshbuff","timestamp":4000,"targetID":7,"abilityGameID":2},
             {"type":"applybuff","timestamp":4000,"targetID":8,"abilityGameID":3},
             {"type":"removebuff","timestamp":5000,"targetID":7,"abilityGameID":2},
             {"type":"death","timestamp":6000,"targetID":7},
             {"type":"death","timestamp":7000,"targetID":8},
             {"type":"applybuff","timestamp":8000,"targetID":7,"abilityGameID":4},
             {"type":"cast","timestamp":11001,"sourceID":7,"abilityGameID":1}]
            """);

        var result = ReportTimelineService.NormalizeEvents(events.Reverse(), 7, 1000, 11000, complete);

        Assert.Equal(complete ? 4 : 3, result.Count);
        Assert.Equal(new TimelineEvent(1, 1000, null, "cast"), result[0]);
        Assert.Equal(new TimelineEvent(2, 2000, 2000, "buff"), result[1]);
        Assert.Equal(new TimelineEvent(0, 5000, null, "death"), result[2]);
        if (complete) Assert.Equal(new TimelineEvent(4, 7000, 3000, "buff", true), result[3]);
    }

    [Fact]
    public void NormalizeEventsIncludesFightBoundariesWithoutInventingUnknownPrepullBuffs()
    {
        var events = ParseEvents("""
            [{"type":"cast","timestamp":1000,"sourceID":7,"abilityGameID":1},
             {"type":"removebuff","timestamp":2000,"targetID":7,"abilityGameID":2},
             {"type":"cast","timestamp":11000,"sourceID":7,"abilityGameID":1}]
            """);
        Assert.Equal(new TimelineEvent[]
        {
            new(1, 0, null, "cast"), new(1, 10000, null, "cast")
        }, ReportTimelineService.NormalizeEvents(events, 7, 1000, 11000, true));
    }

    [Fact]
    public async Task CompleteReportCacheRemovesCoveredPlayerPagesButPreservesOtherReports()
    {
        var metadata = """
            {"data":{"reportData":{"report":{
              "fights":[{"id":1,"startTime":1000,"endTime":11000,"enemyNPCs":[],"phaseTransitions":[]}],
              "masterData":{"actors":[{"id":7,"name":"Player"}],"abilities":[]}
            }}}}
            """;
        var firstPage = EventPage("""[{"type":"cast","timestamp":2000,"sourceID":7,"abilityGameID":1}]""", 3000);
        var lastPage = EventPage("""[{"type":"cast","timestamp":4000,"sourceID":7,"abilityGameID":2}]""");
        using var handler = new QueueHandler(
            TimelineMetadata(), firstPage, lastPage,
            TimelineMetadata(), lastPage,
            metadata, firstPage, lastPage);
        var client = fixture.CreateClient(handler);
        var service = new ReportTimelineService(client, NullLogger<ReportTimelineService>.Instance);
        var original = await service.LoadAsync("report", 1, 7, "Player", "anniversary", default);
        await service.LoadAsync("unrelated", 1, 7, "Player", "anniversary", default);
        var report = Report("report", 1);
        report.Players = [new RankingPlayer { PlayerId = 7, Name = "Player", SpecSlug = "unknown" }];
        await service.PopulateRosterTimelinesAsync(report, "anniversary", false, default);
        foreach (var cursor in new[] { 1000d, 3000d })
        {
            var removed = await client.TryQueryGraphQLFromCacheAsync(
                GraphQlResourceLoader.Load("Timeline.Events.graphql"),
                new
                {
                    code = "report", fightIds = new[] { 1 }, startTime = cursor, endTime = 11000d,
                    filter = "(source.name='Player' AND type='cast') OR (target.name='Player' AND type IN ('applybuff','refreshbuff','removebuff','death'))"
                }, "anniversary");
            Assert.Null(removed);
        }
        var reused = await service.TryLoadCachedAsync("report", 1, 7, "Player", "anniversary", default);
        Assert.Equal(original.Events, reused!.Events);
        Assert.NotNull(await service.TryLoadCachedAsync("unrelated", 1, 7, "Player", "anniversary", default));
        Assert.Equal(8, handler.GraphQlRequests);
    }

    [Fact]
    public async Task FailedReportDownloadPreservesIndividualPlayerCache()
    {
        var metadata = """
            {"data":{"reportData":{"report":{
              "fights":[{"id":1,"startTime":1000,"endTime":11000,"enemyNPCs":[],"phaseTransitions":[]}],
              "masterData":{"actors":[{"id":7,"name":"Player"}],"abilities":[]}
            }}}}
            """;
        using var handler = new QueueHandler(TimelineMetadata(), EventPage("[]"), metadata,
            EventPage("[]", 2000), EventPage("[]", 2000));
        var service = CreateService(handler);
        await service.LoadAsync("report", 1, 7, "Player", "anniversary", default);
        var report = Report("report", 1);
        report.Players = [new RankingPlayer { PlayerId = 7, Name = "Player", SpecSlug = "unknown" }];
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PopulateRosterTimelinesAsync(report, "anniversary", false, default));
        Assert.NotNull(await service.TryLoadCachedAsync("report", 1, 7, "Player", "anniversary", default));
        Assert.Equal(5, handler.GraphQlRequests);
    }

    [Fact]
    public async Task RosterTimelinesShareEveryReportFightAndAllEventPagesWithoutPlayerRequests()
    {
        var metadata = """
            {"data":{"reportData":{"report":{
              "fights":[
                {"id":1,"startTime":1000,"endTime":5000,"enemyNPCs":[{"id":99}],"phaseTransitions":[]},
                {"id":2,"startTime":6000,"endTime":11000,"enemyNPCs":[{"id":99}],"phaseTransitions":[]}
              ],
              "masterData":{"actors":[{"id":7,"name":"Player"},{"id":8,"name":"Other"}],
                "abilities":[{"gameID":1,"name":"Strike"},{"gameID":2,"name":"Boss cast"}]}
            }}}}
            """;
        var pages = new[]
        {
            EventPage("""[{"type":"cast","timestamp":2000,"sourceID":7,"abilityGameID":1}]""", 3000),
            EventPage("""[{"type":"cast","timestamp":4000,"sourceID":8,"abilityGameID":1}]""", 6000),
            EventPage("""[{"type":"cast","timestamp":7000,"sourceID":7,"abilityGameID":1}]""", 8000),
            EventPage("""[{"type":"cast","timestamp":9000,"sourceID":8,"abilityGameID":1},{"type":"cast","timestamp":10000,"sourceID":99,"abilityGameID":2}]""")
        };
        using var handler = new QueueHandler(new[] { metadata }.Concat(pages)
            .Concat(new[] { metadata }).Concat(pages).ToArray());
        var service = CreateService(handler);
        RankingReport RosterReport(int fightId) => new()
        {
            ReportId = "report", Title = "Raid",
            Fights = [new RankingFight { FightId = fightId, Name = "Boss" }],
            Players =
            [
                new RankingPlayer { PlayerId = 7, Name = "Player", SpecSlug = "unknown" },
                new RankingPlayer { PlayerId = 8, Name = "Other", SpecSlug = "unknown" }
            ]
        };
        var first = RosterReport(1);
        await service.PopulateRosterTimelinesAsync(first, "anniversary", false, default);
        Assert.Equal(5, handler.GraphQlRequests);
        Assert.All(first.Players, player => Assert.True(player.Timeline!.IsComplete));
        Assert.Equal(1000, Assert.Single(first.Players[0].Timeline!.Events).Timestamp);
        Assert.Equal(3000, Assert.Single(first.Players[1].Timeline!.Events).Timestamp);
        var variables = handler.Requests[1].GetProperty("variables");
        Assert.Equal(new[] { 1, 2 }, variables.GetProperty("fightIds").EnumerateArray().Select(id => id.GetInt32()));
        Assert.Equal(11000, variables.GetProperty("endTime").GetDouble());

        var second = RosterReport(2);
        await service.PopulateRosterTimelinesAsync(second, "anniversary", false, default);
        Assert.Equal(5, handler.GraphQlRequests);
        Assert.Equal(1000, Assert.Single(second.Players[0].Timeline!.Events).Timestamp);
        Assert.Equal(3000, Assert.Single(second.Players[1].Timeline!.Events).Timestamp);
        Assert.Equal(4000, Assert.Single(second.BossTimeline!.Events).Timestamp);

        // A new service instance must reuse the persisted report cache, not process-local state.
        var specService = new ReportTimelineService(fixture.CreateClient(handler, new WarcraftLogsApiOptions
        {
            BaseUrl = "https://wcl.test/api", AuthUrl = "https://wcl.test/oauth/token",
            ClientId = "test-client", ClientSecret = "test-secret",
            EditionEndpoints = new()
            {
                ["retail"] = new() { BaseUrl = "https://wcl.test/retail", AuthUrl = "https://wcl.test/oauth/token" }
            }
        }), NullLogger<ReportTimelineService>.Instance);
        var specTimeline = await specService.LoadAsync("report", 2, 8, "Other", "anniversary", default);
        var cachedTimeline = await specService.TryLoadCachedAsync("report", 2, 8, "Other", "anniversary", default);
        Assert.Equal(second.Players[1].Timeline!.Events, specTimeline.Events);
        Assert.Equal(specTimeline.Events, cachedTimeline!.Events);
        var boss = await specService.LoadBossAsync("report", 2, "anniversary", false, default);
        Assert.Equal(second.BossTimeline.Events, boss!.Events);
        var phaseReport = Report("report", 2);
        await specService.PopulateFightPhasesAsync([phaseReport], "anniversary", default);
        Assert.NotNull(phaseReport.Fights[0].Phases);
        Assert.Equal(5, handler.GraphQlRequests);
        Assert.Null(await specService.TryLoadCachedAsync("report", 2, 8, "Other", "retail", default));
        Assert.Null(await specService.TryLoadCachedAsync("report", 2, 8, "Wrong", "anniversary", default));
        Assert.Equal(5, handler.GraphQlRequests);

        await service.PopulateRosterTimelinesAsync(second, "anniversary", true, default);
        Assert.Equal(10, handler.GraphQlRequests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrExpiredReportEventPageFallsBackWithoutFetchingReportPages(bool expired)
    {
        var responses = new List<string>
        {
            TimelineMetadata(),
            EventPage("""[{"type":"cast","timestamp":2000,"sourceID":7,"abilityGameID":1}]""", 3000)
        };
        if (expired)
            responses.Add(EventPage("""[{"type":"cast","timestamp":3500,"sourceID":7,"abilityGameID":77777}]"""));
        responses.Add(EventPage("""[{"type":"cast","timestamp":4000,"sourceID":7,"abilityGameID":2}]"""));
        using var handler = new QueueHandler(responses.ToArray());
        var client = fixture.CreateClient(handler);
        await client.QueryGraphQLAsync(GraphQlResourceLoader.Load("Timeline.ReportMetadata.graphql"),
            new { code = "report" }, "anniversary");
        await client.QueryGraphQLAsync(GraphQlResourceLoader.Load("Timeline.Events.graphql"),
            new
            {
                code = "report", fightIds = new[] { 1 }, startTime = 1000d, endTime = 11000d,
                filter = "type IN ('cast','applybuff','refreshbuff','removebuff','death')"
            }, "anniversary");
        if (expired)
        {
            await client.QueryGraphQLAsync(GraphQlResourceLoader.Load("Timeline.Events.graphql"),
                new
                {
                    code = "report", fightIds = new[] { 1 }, startTime = 3000d, endTime = 11000d,
                    filter = "type IN ('cast','applybuff','refreshbuff','removebuff','death')"
                }, "anniversary");
            var cachedPage = Assert.Single(Directory.GetFiles(fixture.CacheDirectory),
                path => File.ReadAllText(path).Contains("77777", StringComparison.Ordinal));
            File.SetLastWriteTimeUtc(cachedPage, DateTime.UtcNow.AddDays(-11));
        }
        var seededRequests = expired ? 3 : 2;
        var service = new ReportTimelineService(client, NullLogger<ReportTimelineService>.Instance);
        Assert.Null(await service.TryLoadCachedAsync("report", 1, 7, "Player", "anniversary", default));
        Assert.Equal(seededRequests, handler.GraphQlRequests);
        var result = await service.LoadAsync("report", 1, 7, "Player", "anniversary", default);
        Assert.Equal(seededRequests + 1, handler.GraphQlRequests);
        Assert.True(result.IsComplete);
        Assert.Equal(2, Assert.Single(result.Events).SpellId);
        Assert.Contains("source.name='Player'", handler.Requests[^1].GetProperty("variables").GetProperty("filter").GetString());
    }

    [Fact]
    public async Task LoadUsesNameFallbackAndEscapesActorNameInUpstreamFilter()
    {
        const string name = "O'Brien\\Mage";
        var metadata = TimelineMetadata(actors: JsonSerializer.Serialize(new[] { new { id = 7, name } }));
        using var handler = new QueueHandler(metadata, EventPage("[]"));

        var result = await CreateService(handler).LoadAsync("report", 1, 99, name, "retail", CancellationToken.None);

        Assert.Equal(7, result.SourceId);
        Assert.Equal(name, result.PlayerName);
        var variables = handler.Requests[1].GetProperty("variables");
        Assert.Contains("source.name='O\\'Brien\\\\Mage'", variables.GetProperty("filter").GetString());
        Assert.Contains("target.name='O\\'Brien\\\\Mage'", variables.GetProperty("filter").GetString());
        Assert.Equal(1000, variables.GetProperty("startTime").GetDouble());
        Assert.Equal(11000, variables.GetProperty("endTime").GetDouble());
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(7, "Wrong")]
    [InlineData(99, null)]
    public async Task LoadRejectsUnknownOrMismatchedPlayerInsteadOfSelectingAnother(int sourceId, string? name)
    {
        using var handler = new QueueHandler(TimelineMetadata());
        var service = CreateService(handler);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.LoadAsync("report", 1, sourceId, name, "retail", CancellationToken.None));
        Assert.Null(await service.TryLoadCachedAsync("report", 1, sourceId, name, "retail", CancellationToken.None));
        Assert.Equal(1, handler.GraphQlRequests);
    }

    [Fact]
    public async Task LoadRejectsAmbiguousNamesEvenWhenTheyDifferOnlyInCase()
    {
        using var handler = new QueueHandler(TimelineMetadata(actors:
            """[{"id":7,"name":"Player"},{"id":8,"name":"PLAYER"}]"""));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            CreateService(handler).LoadAsync("report", 1, null, "Player", "retail", CancellationToken.None));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task LoadReusesSeparateMasterDataWhenFightMetadataIsIncomplete()
    {
        using var handler = new QueueHandler(TimelineMetadata(master: "{}"), """
            {"data":{"reportData":{"report":{"masterData":{"actors":[{"id":7,"name":"Player"}],
            "abilities":[{"gameID":1,"name":"Strike","icon":"strike.jpg"},{"gameID":2,"name":"Unused"}]}}}}}
            """, EventPage("""
            [{"type":"cast","timestamp":2000,"sourceID":7,"abilityGameID":1},
             {"type":"cast","timestamp":3000,"sourceID":7,"abilityGameID":3}]
            """));
        var service = CreateService(handler);

        var result = await service.LoadAsync("report", 1, 7, null, "retail", CancellationToken.None);
        var cached = await service.TryLoadCachedAsync("report", 1, 7, null, "retail", CancellationToken.None);

        Assert.Equal(new TimelineSpell[] { new(3, "Spell 3", null), new(1, "Strike", "strike.jpg") }, result.Spells);
        Assert.NotNull(cached);
        Assert.Equal(result.Spells, cached.Spells);
        Assert.Equal(3, handler.GraphQlRequests);
    }

    [Theory]
    [InlineData(11000)]
    [InlineData(12000)]
    public async Task LoadTreatsCursorAtOrBeyondFightEndAsComplete(double next)
    {
        using var handler = new QueueHandler(TimelineMetadata(), EventPage("[]", next));
        var result = await CreateService(handler).LoadAsync("report", 1, 7, null, "retail", CancellationToken.None);
        Assert.True(result.IsComplete);
        Assert.Equal(2, handler.GraphQlRequests);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(999)]
    public async Task LoadRejectsNonAdvancingPagination(double next)
    {
        using var handler = new QueueHandler(TimelineMetadata(), EventPage("[]", next));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(handler).LoadAsync("report", 1, 7, null, "retail", CancellationToken.None));
        Assert.Equal(2, handler.GraphQlRequests);
    }

    [Fact]
    public async Task LoadLimitsPaginationAndNeverBundlesIncompleteTimelinesFromCache()
    {
        using var handler = new QueueHandler(TimelineMetadata(),
            EventPage("""[{"type":"applybuff","timestamp":2000,"targetID":7,"abilityGameID":1}]""", 4000),
            EventPage("[]", 6000), EventPage("[]", 8000));
        var service = CreateService(handler);

        var fresh = await service.LoadAsync("report", 1, 7, null, "retail", CancellationToken.None);
        var cached = await service.TryLoadCachedAsync("report", 1, 7, null, "retail", CancellationToken.None);

        Assert.False(fresh.IsComplete);
        Assert.Empty(fresh.Events);
        Assert.Null(cached);
        Assert.Equal(4, handler.GraphQlRequests);
    }

    [Fact]
    public async Task CacheOnlyPlayerAndBossMissesNeverFetchUpstream()
    {
        using var handler = new QueueHandler();
        var service = CreateService(handler);
        Assert.Null(await service.TryLoadCachedAsync("report", 1, 7, null, "retail", CancellationToken.None));
        Assert.Null(await service.LoadBossAsync("report", 1, "retail", true, CancellationToken.None));
        Assert.Equal(0, handler.GraphQlRequests);
        Assert.Equal(0, handler.TokenRequests);
    }

    [Fact]
    public async Task CacheOnlyMissingEventPageDoesNotFetchAndReturnsNoPartialTimeline()
    {
        using var handler = new QueueHandler(TimelineMetadata(),
            EventPage("""[{"type":"cast","timestamp":2000,"sourceID":7,"abilityGameID":1}]"""));
        var service = CreateService(handler);
        await service.LoadAsync("report", 1, 7, null, "retail", CancellationToken.None);
        var eventFile = Directory.GetFiles(fixture.CacheDirectory).Single(path => File.ReadAllText(path).Contains("\"events\""));
        File.Delete(eventFile);
        Assert.Null(await service.TryLoadCachedAsync("report", 1, 7, null, "retail", CancellationToken.None));
        Assert.Equal(2, handler.GraphQlRequests);
    }

    [Theory]
    [InlineData(2, 11000)]
    [InlineData(1, 1000)]
    public async Task PlayerAndBossLoadsRejectMissingFightOrInvalidDuration(int id, int end)
    {
        using var handler = new QueueHandler(TimelineMetadata(fightId: id, end: end),
            TimelineMetadata(fightId: id, end: end));
        var service = CreateService(handler);
        if (id == 2)
        {
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                service.LoadAsync("report", 1, 7, null, "retail", CancellationToken.None));
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                service.LoadBossAsync("report", 1, "retail", false, CancellationToken.None));
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.LoadAsync("report", 1, 7, null, "retail", CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.LoadBossAsync("report", 1, "retail", false, CancellationToken.None));
        }
    }

    [Fact]
    public async Task BossLoadFiltersEventsAndPhasesAndKeepsEditionSpecificObservedPhases()
    {
        const string metadata = """
            {"data":{"reportData":{"report":{"fights":[{"id":1,"startTime":1000,"endTime":11000,
            "encounterID":3470,"phaseTransitions":[{"id":3,"startTime":7000},
            {"id":2,"startTime":999},{"id":4,"startTime":11001}]}],"masterData":{"abilities":[]}}}}}
            """;
        using var handler = new QueueHandler(metadata, EventPage("""
            [{"type":"applybuff","timestamp":2000,"abilityGameID":1},
             {"type":"cast","timestamp":2000,"abilityGameID":0},
             {"type":"cast","timestamp":999,"abilityGameID":1},
             {"type":"cast","timestamp":11001,"abilityGameID":1},
             {"type":"cast","timestamp":5000,"abilityGameID":1}]
            """));

        var result = await CreateService(handler).LoadBossAsync("report", 1, "anniversary", false, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Boss", result.Name);
        Assert.Equal(new TimelinePhase(3, 6000), Assert.Single(result.Phases));
        Assert.Equal(new TimelineEvent(1, 4000, null, "cast"), Assert.Single(result.Events));
        Assert.Equal(new TimelineSpell(1, "Spell 1", null), Assert.Single(result.Spells));
    }

    [Fact]
    public async Task PopulateFightPhasesQueriesOnlyUnresolvedReportsAndFightsThenReusesResults()
    {
        const string response = """
            {"data":{"reportData":{"r0":{"fights":[{"id":2,"encounterID":45,
              "startTime":1000,"endTime":5000,"phaseTransitions":[]}]}}}}
            """;
        using var handler = new QueueHandler(response);
        var service = CreateService(handler);
        var resolved = Report("alreadyResolved", 1);
        resolved.Fights[0].Phases = [];
        var mixed = Report("mixed", 2);
        mixed.Fights.Add(new RankingFight { FightId = 3, Name = "Encounter", Phases = [] });
        var reports = new List<RankingReport> { resolved, mixed };

        await service.PopulateFightPhasesAsync(reports, "retail", CancellationToken.None);
        await service.PopulateFightPhasesAsync(reports, "retail", CancellationToken.None);

        var variables = Assert.Single(handler.Requests).GetProperty("variables");
        Assert.Equal("mixed", variables.GetProperty("code0").GetString());
        Assert.Equal(2, Assert.Single(variables.GetProperty("fights0").EnumerateArray()).GetInt32());
        Assert.False(variables.TryGetProperty("code1", out _));
        Assert.All(reports.SelectMany(report => report.Fights), fight => Assert.NotNull(fight.Phases));
    }

    [Fact]
    public async Task PopulateFightPhasesKeepsHealthyAliasesFromPartialResponses()
    {
        const string response = """
            {"data":{"reportData":{
              "r0":{"fights":[{"id":22,"encounterID":45,"startTime":1000,"endTime":5000,
                "phaseTransitions":[{"id":2,"startTime":2000}]}]}
              ,"r1":null
            }},
            "errors":[{"message":"This report is private","path":["reportData","r1"]}]}
            """;
        using var handler = new QueueHandler(response);
        var service = CreateService(handler);
        var reports = new List<RankingReport> { Report("private", 11), Report("healthy", 22) };

        await service.PopulateFightPhasesAsync(reports, "retail", CancellationToken.None);

        var report = Assert.Single(reports);
        Assert.Equal("healthy", report.ReportId);
        var fight = Assert.Single(report.Fights);
        Assert.Equal(45, fight.EncounterId);
        Assert.Equal(1000, Assert.Single(fight.Phases!).Timestamp);
    }

    [Fact]
    public async Task LoadNullReportThrowsTypedMissingReportError()
    {
        using var handler = new QueueHandler("""{"data":{"reportData":{"report":null}}}""");
        var service = CreateService(handler);

        var failure = await Assert.ThrowsAsync<WarcraftLogsReportNotFoundException>(() =>
            service.LoadAsync("missing", 3, 5, "Player", "retail", CancellationToken.None));

        Assert.Contains("not found", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PopulateFightPhasesRetriesPathlessFailuresPerReport()
    {
        const string unavailable = """{"errors":[{"message":"You do not have permission to view this report."}],"data":null}""";
        const string healthy = """
            {"data":{"reportData":{"r0":{"fights":[{"id":44,"encounterID":45,
            "startTime":1000,"endTime":5000,"phaseTransitions":[{"id":2,"startTime":2000}]}]}}}}
            """;
        using var handler = new QueueHandler(unavailable, unavailable, healthy);
        var service = CreateService(handler);
        var reports = new List<RankingReport> { Report("aPrivatePathless", 33), Report("bHealthyPathless", 44) };

        await service.PopulateFightPhasesAsync(reports, "retail", CancellationToken.None);

        var report = Assert.Single(reports);
        Assert.Equal("bHealthyPathless", report.ReportId);
        Assert.Equal(1000, Assert.Single(Assert.Single(report.Fights).Phases!).Timestamp);
        Assert.Equal(3, handler.GraphQlRequests);
    }

    [Fact]
    public async Task LoadAndTryLoadCachedApplyIdenticalPlayerCorrectionsWithoutAdditionalRequests()
    {
        const string metadata = """
            {"data":{"reportData":{"report":{"fights":[{"id":77,"startTime":10000,"endTime":110000}],
            "masterData":{"actors":[{"id":7,"name":"Healer"}],
            "abilities":[{"gameID":114052,"name":"Ascendance","icon":"ascendance.jpg"}]}}}}}
            """;
        const string events = """
            {"data":{"reportData":{"report":{"events":{"data":[
            {"type":"applybuff","timestamp":10000,"targetID":7,"abilityGameID":114052},
            {"type":"removebuff","timestamp":16000,"targetID":7,"abilityGameID":114052},
            {"type":"applybuff","timestamp":105000,"targetID":7,"abilityGameID":114052}
            ],"nextPageTimestamp":null}}}}}
            """;
        using var handler = new QueueHandler(metadata, events);
        var service = CreateService(handler);

        var fresh = await service.LoadAsync("correctionPlayer", 77, 7, "Healer", "retail",
            CancellationToken.None, 264);
        var cached = await service.TryLoadCachedAsync("correctionPlayer", 77, 7, "Healer", "retail",
            CancellationToken.None, 264);

        Assert.Equal(378270, fresh.Events[0].SpellId);
        Assert.Contains(fresh.Spells, spell => spell.Name == "Deeply Rooted Elements");
        Assert.Equal(114052, fresh.Events[1].SpellId);
        Assert.True(fresh.Events[1].IsInferred);
        Assert.NotNull(cached);
        Assert.Equal(fresh.Events, cached.Events);
        Assert.Equal(2, handler.GraphQlRequests);
    }

    [Fact]
    public async Task LoadBossAppliesIdenticalFreshAndCachedPhasesWithRelativeTimesAndNoteAnchors()
    {
        const string metadata = """
            {"data":{"reportData":{"report":{"fights":[{"id":88,"name":"Nek'zali the Soulcoiler",
            "startTime":10000,"endTime":110000,"phaseTransitions":[
            {"id":2,"startTime":30000},{"id":3,"startTime":100000}]}],
            "masterData":{"abilities":[]}}}}}
            """;
        const string events = """
            {"data":{"reportData":{"report":{"events":{"data":[
            {"type":"cast","timestamp":30000,"abilityGameID":1295124},
            {"type":"cast","timestamp":85000,"abilityGameID":1292248}
            ],"nextPageTimestamp":null}}}}}
            """;
        using var handler = new QueueHandler(metadata, events);
        var service = CreateService(handler);

        var fresh = await service.LoadBossAsync("correctionBoss", 88, "retail", false, CancellationToken.None);
        var cached = await service.LoadBossAsync("correctionBoss", 88, "retail", true, CancellationToken.None);

        Assert.NotNull(fresh);
        Assert.Equal(new double[] { 0, 20_000, 60_000, 90_000 }, fresh.Phases.Select(phase => phase.Timestamp));
        Assert.Equal(3, fresh.Phases[^1].NotePhaseId);
        Assert.NotNull(cached);
        Assert.Equal(fresh.Phases, cached.Phases);
        Assert.Equal(2, handler.GraphQlRequests);
    }

    private ReportTimelineService CreateService(QueueHandler handler) =>
        new(fixture.CreateClient(handler), NullLogger<ReportTimelineService>.Instance);

    private static RankingReport Report(string code, int fightId) => new()
    {
        ReportId = code,
        Title = code,
        Fights = [new RankingFight { FightId = fightId, Name = "Encounter" }]
    };

    private static JsonElement[] ParseEvents(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateArray().Select(item => item.Clone()).ToArray();
    }

    private static string TimelineMetadata(
        string actors = """[{"id":7,"name":"Player"}]""", string? master = null, int fightId = 1, int end = 11000) =>
        JsonSerializer.Serialize(new
        {
            data = new
            {
                reportData = new
                {
                    report = new
                    {
                        fights = new[] { new { id = fightId, startTime = 1000, endTime = end } },
                        masterData = master is null
                            ? JsonSerializer.SerializeToElement(new { actors = JsonSerializer.Deserialize<JsonElement>(actors), abilities = Array.Empty<object>() })
                            : JsonSerializer.Deserialize<JsonElement>(master)
                    }
                }
            }
        });

    private static string EventPage(string events, double? next = null) =>
        JsonSerializer.Serialize(new
        {
            data = new
            {
                reportData = new
                {
                    report = new
                    {
                        events = new { data = JsonSerializer.Deserialize<JsonElement>(events), nextPageTimestamp = next }
                    }
                }
            }
        });
}
