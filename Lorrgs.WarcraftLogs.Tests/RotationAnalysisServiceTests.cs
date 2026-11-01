using System.Text.Json;
using Lorrgs.Tests.TestSupport;
using Lorrgs.WarcraftLogs.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lorrgs.WarcraftLogs.Tests;

public sealed class RotationAnalysisServiceTests : IDisposable
{
    private readonly TestClientFixture fixture = new();
    public void Dispose() => fixture.Dispose();

    [Fact]
    public async Task AnalyzePaginatesSortsAndSummarizesCastsWithMedianIntervals()
    {
        using var handler = new QueueHandler(Metadata(), Page([
            Event(7000, 1, "Strike"), Event(1000, 1, "Strike"), Event(4000, 2, "Heal")
        ], 8000.2), Page([Event(16000, 1, "Strike"), Event(11000, 1, "Strike")]), Page([]));
        var result = await Service(handler).AnalyzeAsync(new() { ReportCode = "  report  ", SourceId = 7 });

        Assert.Equal("report", result.ReportCode);
        Assert.Equal(1, result.FightId);
        Assert.Equal("Boss", result.FightName);
        Assert.Equal("Player", result.PlayerName);
        Assert.Equal(7, result.SourceId);
        Assert.Equal("Retribution", result.SpecHint);
        Assert.Equal("dps", result.RoleHint);
        Assert.Equal("Expansion", result.GameExpansionHint);
        Assert.Equal(60, result.FightDurationSeconds);
        Assert.Equal(5, result.CastsPerMinute);
        Assert.Equal(new long[] { 1000, 4000, 7000, 11000, 16000 }, result.RotationTimeline.Select(point => point.Timestamp));
        var strike = Assert.Single(result.AbilitySummary, ability => ability.SpellId == 1);
        Assert.Equal(4, strike.CastCount);
        Assert.Equal(80, strike.SharePercent);
        Assert.Equal(5, strike.MedianIntervalSeconds);
        Assert.Null(Assert.Single(result.AbilitySummary, ability => ability.SpellId == 2).MedianIntervalSeconds);
        var request = handler.Requests[2].GetProperty("variables");
        Assert.Equal(8001, request.GetProperty("startTime").GetInt64());
        Assert.Equal(61000, request.GetProperty("endTime").GetInt64());
        Assert.Equal(7, request.GetProperty("sourceId").GetInt32());
        Assert.Equal(1, request.GetProperty("fightIds")[0].GetInt32());
    }

    [Fact]
    public async Task AnalyzeBuildsPhaseAwareRotationSummariesFromPhaseTransitions()
    {
        const string fights = """
            [{"id":1,"name":"Boss","startTime":1000,"endTime":61000,"kill":true,
              "phaseTransitions":[{"id":2,"startTime":21000},{"id":3,"startTime":41000}]}]
            """;
        using var handler = new QueueHandler(
            Metadata(fights),
            Page([
                Event(2000, 101, "Strike"),
                Event(5000, 102, "Judgment"),
                Event(23000, 201, "Crusader Strike"),
                Event(26000, 202, "Consecration"),
                Event(43000, 301, "Avenging Wrath"),
                Event(45000, 302, "Potion")
            ]),
            Page([]));

        var result = await Service(handler).AnalyzeAsync(new() { ReportCode = "report", FightId = 1 });

        Assert.Equal(new[] { 1, 2, 3 }, result.Phases.Select(phase => (int)phase.Id).ToArray());
        Assert.Equal(3, result.PhaseSummaries.Count);
        Assert.Contains(result.Notes, note => note.Contains("Phase-aware rotation summary"));
        Assert.Equal(1, result.PhaseSummaries[0].PhaseId);
        Assert.Equal(2, result.PhaseSummaries[1].PhaseId);
        Assert.Equal(3, result.PhaseSummaries[2].PhaseId);
        Assert.Contains("spell #101", result.PhaseSummaries[0].RotationPattern);
        Assert.Contains("spell #201", result.PhaseSummaries[1].RotationPattern);
        Assert.Contains("spell #301", result.PhaseSummaries[2].RotationPattern);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(20000, 10000)]
    public async Task AnalyzeClampsUpstreamEventLimit(int requested, int expected)
    {
        using var handler = new QueueHandler(Metadata(), Page([]), Page([]));
        await Service(handler).AnalyzeAsync(new() { ReportCode = "report", EventLimit = requested });
        Assert.Equal(expected, handler.Requests[1].GetProperty("variables").GetProperty("limit").GetInt32());
        Assert.Equal(3, handler.GraphQlRequests);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public async Task AnalyzeStopsWhenPaginationFailsToAdvance(double nextTimestamp)
    {
        using var handler = new QueueHandler(Metadata(), Page([Event(1000, 1, "Strike")], nextTimestamp), Page([]));
        var result = await Service(handler).AnalyzeAsync(new() { ReportCode = "report" });
        Assert.Single(result.RotationTimeline);
        Assert.Equal(3, handler.GraphQlRequests);
    }

    [Fact]
    public async Task AnalyzeStopsAtEventLimitAndBoundsResponseTimelineAndAbilitySummary()
    {
        using var handler = new QueueHandler(Metadata(),
            Page(Enumerable.Range(0, 240).Select(i => Event(i + 1000, i % 30, $"Spell {i % 30}")), 2000),
            Page([]));
        var result = await Service(handler).AnalyzeAsync(new() { ReportCode = "report", EventLimit = 100 });
        Assert.Equal(200, result.RotationTimeline.Count);
        Assert.Equal(20, result.AbilitySummary.Count);
        Assert.Equal(240, result.CastsPerMinute);
        Assert.Equal(3, handler.GraphQlRequests);
    }

    [Theory]
    [InlineData(30, 8, 85, 490, 98, "optimal")]
    [InlineData(18, 4, 60, null, 60, "needs_improvement")]
    [InlineData(10, 3, 40, 530, 40, "needs_improvement")]
    [InlineData(0, 0, null, null, 40, "needs_context")]
    [InlineData(25, 4, 70, 499, 78, "optimal")]
    public async Task AnalyzeScoresActivityAbilityBreadthParseAndGear(
        int casts, int abilities, int? percentile, int? itemLevel, int score, string verdict)
    {
        using var handler = new QueueHandler(Metadata(),
            Page(Enumerable.Range(0, casts).Select(i => Event(i * 1000 + 1000, i % abilities, $"Spell {i % abilities}"))),
            Page([]));
        var result = await Service(handler).AnalyzeAsync(new()
        {
            ReportCode = "report", ParsePercentile = percentile, ItemLevel = itemLevel
        });
        Assert.Equal(score, result.Score);
        Assert.Equal(verdict, result.Verdict);
        Assert.Equal(percentile, result.ParsePercentile);
        Assert.Equal(itemLevel, result.ItemLevel);
        Assert.NotEmpty(result.Notes);
        if (percentile is null) Assert.Contains(result.Notes, note => note.Contains("heuristic-only and capped"));
        if (itemLevel >= 520) Assert.Contains(result.Notes, note => note.Contains("High-ilvl underperformance warning"));
    }

    [Theory]
    [InlineData("Holy", "healer")]
    [InlineData("Restoration", "healer")]
    [InlineData("Discipline", "healer")]
    [InlineData("Mistweaver", "healer")]
    [InlineData("Preservation", "healer")]
    [InlineData("Protection", "tank")]
    [InlineData("Blood", "tank")]
    [InlineData("Guardian", "tank")]
    [InlineData("Vengeance", "tank")]
    [InlineData("Brewmaster", "tank")]
    [InlineData("Retribution", "dps")]
    public async Task AnalyzeInfersRoleFromSelectedActor(string spec, string role)
    {
        using var handler = new QueueHandler(Metadata(actors: $$"""[{"id":7,"name":"Player","subType":"{{spec}}"}]"""), Page([]), Page([]));
        var result = await Service(handler).AnalyzeAsync(new() { ReportCode = "report", PlayerName = "pLaYeR" });
        Assert.Equal(spec, result.SpecHint);
        Assert.Equal(role, result.RoleHint);
    }

    [Theory]
    [InlineData("Holy", "dataType: Healing")]
    [InlineData("Retribution", "dataType: DamageDone")]
    public async Task AnalyzeUsesRoleAppropriateThroughputQuery(string spec, string expectedDataType)
    {
        using var handler = new QueueHandler(
            Metadata(actors: $$"""[{"id":7,"name":"Player","subType":"{{spec}}"}]"""),
            Page([]),
            Page([]));

        await Service(handler).AnalyzeAsync(new() { ReportCode = "report" });

        var throughputQuery = handler.Requests[2].GetProperty("query").GetString();
        Assert.Contains(expectedDataType, throughputQuery);
    }

    [Fact]
    public async Task AnalyzeInfersHealerRoleFromThroughputWhenSpecIsClassOnly()
    {
        using var handler = new QueueHandler(
            Metadata(actors: """[{"id":7,"name":"Player","subType":"Shaman"}]"""),
            Page([Event(1000, 101, "Spell 101")]),
            Page([ThroughputEvent(2000, 1064, "Chain Heal", 12000)]),
            Page([ThroughputEvent(2500, 403, "Lightning Bolt", 2000)]));

        var result = await Service(handler).AnalyzeAsync(new() { ReportCode = "report" });

        Assert.Equal("healer", result.RoleHint);
        Assert.Contains(result.Notes, note => note.Contains("Detected healer playstyle"));
        Assert.Equal(4, handler.GraphQlRequests);
    }

    [Theory]
    [InlineData(null, 2)]
    [InlineData(1, 1)]
    public async Task AnalyzePrefersLongestKillUnlessFightIsExplicit(int? requestedFight, int expectedFight)
    {
        const string fights = """
            [{"id":1,"name":"Long wipe","startTime":0,"endTime":120000,"kill":false},
             {"id":2,"name":"Long kill","startTime":1000,"endTime":61000,"kill":true},
             {"id":3,"name":"Short kill","startTime":0,"endTime":10000,"kill":true},
             {"id":4,"name":"Invalid","startTime":5,"endTime":4}]
            """;
        using var handler = new QueueHandler(Metadata(fights), Page([]), Page([]));
        var result = await Service(handler).AnalyzeAsync(new() { ReportCode = "report", FightId = requestedFight });
        Assert.Equal(expectedFight, result.FightId);
    }

    [Fact]
    public async Task AnalyzeWithoutKillsChoosesLongestPullAndUsesFallbackMetadata()
    {
        using var handler = new QueueHandler(Metadata(
            """[{"id":1,"name":"Short","startTime":0,"endTime":100},{"id":2,"name":null,"startTime":0,"endTime":500}]""",
            """[{"ignored":true},{"id":8,"name":null}]""", zone: "{}"), Page([]), Page([]));
        var result = await Service(handler).AnalyzeAsync(new() { ReportCode = "report" });
        Assert.Equal(2, result.FightId);
        Assert.Equal("Unknown", result.FightName);
        Assert.Equal("", result.PlayerName);
        Assert.Equal("Unknown", result.SpecHint);
        Assert.Null(result.GameExpansionHint);
        Assert.Equal(1, result.FightDurationSeconds);
    }

    [Fact]
    public async Task AnalyzeSkipsIncompleteEventsAndProvidesAbilityAndTypeFallbacks()
    {
        using var handler = new QueueHandler(Metadata(), """
            {"data":{"reportData":{"report":{"events":{"data":[
              {"timestamp":1},{"abilityGameID":"bad","timestamp":2},{"abilityGameID":7},
              {"abilityGameID":8,"timestamp":1000},
              {"abilityGameID":9,"timestamp":2000,"ability":{"name":null},"type":"cast"}
            ]}}}}}
            """, Page([]));
        var result = await Service(handler).AnalyzeAsync(new() { ReportCode = "report" });
        Assert.Equal(2, result.RotationTimeline.Count);
        Assert.Equal("Spell:8", result.RotationTimeline[0].Ability);
        Assert.Equal("cast", result.RotationTimeline[0].EventType);
        Assert.Equal("Unknown", result.RotationTimeline[1].Ability);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"reportData\":{}}")]
    [InlineData("{\"reportData\":{\"report\":{}}}")]
    [InlineData("{\"reportData\":{\"report\":{\"events\":{}}}}")]
    [InlineData("{\"reportData\":{\"report\":{\"events\":{\"data\":{}}}}}")]
    public async Task AnalyzeMissingEventCollectionsProduceEmptyAnalysis(string payload)
    {
        using var handler = new QueueHandler(Metadata(), $$"""{"data":{{payload}}}""", Page([]));
        var result = await Service(handler).AnalyzeAsync(new() { ReportCode = "report" });
        Assert.Empty(result.RotationTimeline);
        Assert.Empty(result.AbilitySummary);
        Assert.Equal(0, result.CastsPerMinute);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task AnalyzeRejectsBlankReportCodesWithoutNetworkRequests(string reportCode)
    {
        using var handler = new QueueHandler();
        await Assert.ThrowsAsync<ArgumentException>(() => Service(handler).AnalyzeAsync(new() { ReportCode = reportCode }));
        Assert.Equal(0, handler.GraphQlRequests);
    }

    [Theory]
    [InlineData("{}", "Report not found")]
    [InlineData("{\"reportData\":{}}", "Report not found")]
    [InlineData("{\"reportData\":{\"report\":null}}", "Report not found")]
    [InlineData("{\"reportData\":{\"report\":{}}}", "No fights found")]
    [InlineData("{\"reportData\":{\"report\":{\"fights\":[]}}}", "No valid fights")]
    [InlineData("{\"reportData\":{\"report\":{\"fights\":[{\"id\":1,\"name\":\"Bad\",\"startTime\":5,\"endTime\":4}]}}}", "No valid fights")]
    [InlineData("{\"reportData\":{\"report\":{\"fights\":[{\"id\":1,\"name\":\"Boss\",\"startTime\":0,\"endTime\":10}]}}}", "No actors found")]
    [InlineData("{\"reportData\":{\"report\":{\"fights\":[{\"id\":1,\"name\":\"Boss\",\"startTime\":0,\"endTime\":10}],\"masterData\":{\"actors\":[]}}}}", "No players found")]
    public async Task AnalyzeSurfacesMissingMetadataErrors(string metadata, string expectedMessage)
    {
        using var handler = new QueueHandler($$"""{"data":{{metadata}}}""");
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Service(handler).AnalyzeAsync(new() { ReportCode = "report" }));
        Assert.Contains(expectedMessage, failure.Message);
        Assert.Equal(1, handler.GraphQlRequests);
    }

    [Theory]
    [InlineData(99, null, null, "Fight id 99")]
    [InlineData(null, 99, null, "sourceId 99")]
    [InlineData(null, null, "Missing", "Player 'Missing'")]
    public async Task AnalyzeSurfacesExplicitSelectionErrors(int? fightId, int? sourceId, string? name, string message)
    {
        using var handler = new QueueHandler(Metadata());
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(handler).AnalyzeAsync(
            new() { ReportCode = "report", FightId = fightId, SourceId = sourceId, PlayerName = name }));
        Assert.Contains(message, failure.Message);
        Assert.Equal(1, handler.GraphQlRequests);
    }

    private RotationAnalysisService Service(QueueHandler handler) =>
        new(NullLogger<RotationAnalysisService>.Instance, fixture.CreateClient(handler));

    private static string Metadata(
        string fights = """[{"id":1,"name":"Boss","startTime":1000,"endTime":61000,"kill":true}]""",
        string actors = """[{"id":7,"name":"Player","subType":"Retribution"}]""",
        string zone = """{"name":"Expansion"}""") =>
        JsonSerializer.Serialize(new
        {
            data = new
            {
                reportData = new
                {
                    report = new
                    {
                        fights = JsonSerializer.Deserialize<JsonElement>(fights),
                        masterData = new { actors = JsonSerializer.Deserialize<JsonElement>(actors) },
                        zone = JsonSerializer.Deserialize<JsonElement>(zone)
                    }
                }
            }
        });

    private static object Event(long timestamp, int spell, string ability) =>
        new { timestamp, abilityGameID = spell, ability = new { name = ability }, type = "cast" };

    private static object ThroughputEvent(long timestamp, int spell, string ability, double amount) =>
        new { timestamp, abilityGameID = spell, ability = new { name = ability }, amount, type = "damage" };

    private static string Page(IEnumerable<object> events, double? next = null) =>
        JsonSerializer.Serialize(new { data = new { reportData = new { report = new { events = new { data = events, nextPageTimestamp = next } } } } });
}
