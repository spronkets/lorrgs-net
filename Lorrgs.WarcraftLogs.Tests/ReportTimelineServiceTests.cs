using System.Text.Json;
using Lorrgs.Tests.TestSupport;
using Lorrgs.WarcraftLogs.Configuration;
using Lorrgs.WarcraftLogs.Models;
using Lorrgs.WarcraftLogs.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;

namespace Lorrgs.WarcraftLogs.Tests;

public sealed class ReportTimelineServiceTests : IDisposable
{
    private readonly TestClientFixture fixture = new();
    public void Dispose() => fixture.Dispose();

    [Theory]
    [InlineData("ValidReport123", "ValidReport123")]
    [InlineData("bad\r\nforged", "[invalid]")]
    public async Task UnavailablePhaseReportsLogValidatedIdentity(string reportCode, string loggedCode)
    {
        using var handler = new QueueHandler("""{"data":{"reportData":{"r0":null}}}""");
        var logger = new RecordingLogger();
        var service = new ReportTimelineService(fixture.CreateClient(handler), logger);
        var reports = new List<RankingReport>
        {
            new() { ReportId = reportCode, Title = "Phase report",
                Fights = [new RankingFight { FightId = 42, Name = "Boss" }] }
        };
        await service.PopulateFightPhasesAsync(reports, "retail", default);
        Assert.Empty(reports);
        Assert.Contains(logger.Messages, message =>
            message.Contains($"Removing inaccessible ranking report {loggedCode} fights=42", StringComparison.Ordinal));
        Assert.All(logger.Messages, message => Assert.DoesNotContain("\r\n", message));
    }

    [Theory]
    [InlineData("\r")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\u0085")]
    [InlineData("\u2028")]
    [InlineData("\u2029")]
    public async Task EmptyTimelineLogsSourceMetadataInsteadOfRequestValues(string separator)
    {
        var reportCode = $"report{separator}forged";
        using var handler = new QueueHandler(TimelineMetadata(), EventPage("[]"));
        var logger = new RecordingLogger();
        var service = new ReportTimelineService(fixture.CreateClient(handler), logger);

        var result = await service.LoadAsync(reportCode, 1, 7, null, "retail", CancellationToken.None);

        Assert.Equal(reportCode, result.ReportCode);
        Assert.Contains(logger.Messages, message => message.StartsWith("WarcraftLogs returned no matching timeline events", StringComparison.Ordinal));
        Assert.All(logger.Messages, message => Assert.DoesNotContain(reportCode, message));
        var scope = Assert.Single(logger.Scopes);
        Assert.Equal("verified-report", scope["ReportCode"]);
        Assert.Equal("Verified report", scope["ReportTitle"]);
        Assert.Equal(1d, scope["FightId"]);
        Assert.All(handler.Requests, request =>
            Assert.Equal(reportCode, request.GetProperty("variables").GetProperty("code").GetString()));
    }

    [Fact]
    public async Task MissingSourceReportCodeIsNotReplacedByRequestCodeInLogs()
    {
        var metadata = System.Text.Json.Nodes.JsonNode.Parse(TimelineMetadata())!;
        metadata["data"]!["reportData"]!["report"]!.AsObject().Remove("code");
        using var handler = new QueueHandler(metadata.ToJsonString(), EventPage("[]"));
        var logger = new RecordingLogger();
        var service = new ReportTimelineService(fixture.CreateClient(handler), logger);

        var result = await service.LoadAsync("request-only-code", 1, 7, null, "retail", CancellationToken.None);

        Assert.Equal("request-only-code", result.ReportCode);
        Assert.Null(Assert.Single(logger.Scopes)["ReportCode"]);
        Assert.All(logger.Messages, message => Assert.DoesNotContain("request-only-code", message));
    }

    [Fact]
    public async Task UnresolvedPlayerLogsFoundReportContextWithoutLoggingRequestedPlayer()
    {
        const string requestedPlayer = "unverified\r\nsecret-player";
        using var handler = new QueueHandler(TimelineMetadata());
        var logger = new RecordingLogger();
        var service = new ReportTimelineService(fixture.CreateClient(handler), logger);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.LoadAsync("request-code", 1, null, requestedPlayer, "retail", CancellationToken.None));

        Assert.Contains(logger.Messages, message => message.StartsWith("Timeline player could not be resolved", StringComparison.Ordinal));
        Assert.All(logger.Messages, message =>
        {
            Assert.DoesNotContain(requestedPlayer, message);
            Assert.DoesNotContain("request-code", message);
        });
        var scope = Assert.Single(logger.Scopes);
        Assert.Equal("verified-report", scope["ReportCode"]);
        Assert.DoesNotContain(scope.Values, value => Equals(value, requestedPlayer) || Equals(value, "request-code"));
        Assert.Equal(1, handler.GraphQlRequests);
    }

    [Fact]
    public async Task PageLimitWarningUsesSourceMetadataAndPreservesIncompleteTimeline()
    {
        const string reportCode = "report\r\n\u0085\u2028\u2029forged";
        using var handler = new QueueHandler(TimelineMetadata(),
            EventPage("[]", 4000), EventPage("[]", 6000), EventPage("[]", 8000));
        var logger = new RecordingLogger();
        var service = new ReportTimelineService(fixture.CreateClient(handler), logger);

        var result = await service.LoadAsync(reportCode, 1, 7, null, "retail", CancellationToken.None);

        Assert.False(result.IsComplete);
        Assert.Equal(reportCode, result.ReportCode);
        var warning = Assert.Single(logger.Messages, message =>
            message.StartsWith("Timeline page limit reached", StringComparison.Ordinal));
        Assert.DoesNotContain(reportCode, warning);
        Assert.Equal("verified-report", Assert.Single(logger.Scopes)["ReportCode"]);
        Assert.Equal(4, handler.GraphQlRequests);
        Assert.All(handler.Requests, request =>
            Assert.Equal(reportCode, request.GetProperty("variables").GetProperty("code").GetString()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PhaseBatchesStartTogetherAndMergeUnavailableReportsSafely(bool cancel)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = 0;
        using var handler = new CallbackHandler(async (request, cancellationToken) =>
        {
            if (request.RequestUri!.AbsolutePath == "/oauth/token")
                return """{"access_token":"token","expires_in":3600}""";
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var variables = document.RootElement.GetProperty("variables");
            if (Interlocked.Increment(ref requests) == 2) started.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            var data = new Dictionary<string, object?>();
            for (var index = 0; variables.TryGetProperty($"code{index}", out var code); index++)
                data[$"r{index}"] = code.GetString() == "phase20" ? null :
                    new { fights = new[] { new { id = 1, startTime = 0, endTime = 10000, phaseTransitions = Array.Empty<object>() } } };
            return JsonSerializer.Serialize(new { data = new { reportData = data } });
        });
        var reports = Enumerable.Range(0, 21).Select(index => new RankingReport
        {
            ReportId = $"phase{index:D2}", Title = "Concurrent phase test",
            Fights = [new RankingFight { FightId = 1, Name = "Boss" }]
        }).ToList();
        var service = new ReportTimelineService(fixture.CreateClient(handler), NullLogger<ReportTimelineService>.Instance);
        using var cancellation = new CancellationTokenSource();
        var task = service.PopulateFightPhasesAsync(reports, "retail", cancellation.Token);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(task.IsCompleted);
            if (cancel) cancellation.Cancel();
        }
        finally
        {
            release.TrySetResult();
            if (cancel)
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
            else
                await task;
        }
        Assert.Equal(2, requests);
        if (!cancel)
        {
            Assert.Equal(20, reports.Count);
            Assert.All(reports, report => Assert.NotNull(report.Fights[0].Phases));
        }
    }

    [Fact]
    public void NormalizeHealsIncludesRecordedPercentageTransitionsWithoutMixingActors()
    {
        var events = ParseEvents("""
            [{"type":"damage","timestamp":1000,"sourceID":8,"targetID":7,"abilityGameID":1,
              "resourceActor":2,"hitPoints":40,"maxHitPoints":100},
             {"type":"cast","timestamp":1100,"sourceID":8,"abilityGameID":2,
              "resourceActor":1,"hitPoints":90,"maxHitPoints":100},
             {"type":"heal","timestamp":1200,"sourceID":8,"targetID":7,"abilityGameID":3,
              "amount":3000,"resourceActor":2,"hitPoints":70,"maxHitPoints":100},
             {"type":"heal","timestamp":1300,"sourceID":8,"targetID":7,"abilityGameID":3,
              "amount":1000,"tick":true,"resourceActor":2,"hitPoints":80,"maxHitPoints":100}]
            """);
        var heals = ReportTimelineService.NormalizeEvents(events, 7, 1000, 2000, true)
            .Where(item => item.Kind is "heal" or "hot").ToArray();
        Assert.Equal(40, heals[0].HealthBeforePercent);
        Assert.Equal(70, heals[0].HealthAfterPercent);
        Assert.True(heals[0].HealthBeforeIsPreviousSnapshot);
        Assert.Equal(70, heals[1].HealthBeforePercent);
        Assert.Equal(80, heals[1].HealthAfterPercent);
        Assert.True(heals[1].HealthBeforeIsPreviousSnapshot);
        Assert.Equal(10000, ReportTimelineService.EstimateMaxHealth(heals));
    }

    [Fact]
    public void EstimateMaxHealthUsesMedianAndRejectsSmallOrUnknownPercentageGains()
    {
        TimelineEvent Heal(double amount, double? before, double? after, bool previous = true) =>
            new(1, 1000, null, "heal", Amount: amount, HealthBeforePercent: before,
                HealthAfterPercent: after, HealthBeforeIsPreviousSnapshot: previous);
        var events = new[]
        {
            Heal(3000, 40, 70), Heal(2000, 50, 70), Heal(9000, 40, 70),
            Heal(5000, 90, 95), Heal(3000, null, 70), Heal(3000, 40, null),
            Heal(3000, 80, 70), Heal(0, 40, 70), Heal(6000, 40, 70, false)
        };
        Assert.Equal(10000, ReportTimelineService.EstimateMaxHealth(events));
        Assert.Equal(15000, ReportTimelineService.EstimateMaxHealth([Heal(3000, 40, 70), Heal(6000, 40, 70)]));
        Assert.Null(ReportTimelineService.EstimateMaxHealth([Heal(1000, 50, 59.9)]));
        Assert.Equal(10000, ReportTimelineService.EstimateMaxHealth([Heal(1000, 50, 60)]));
    }

    [Fact]
    public async Task LoadFetchesAndCachesEstimatedHealthSeparatelyFromRecordedMaximum()
    {
        using var handler = new QueueHandler(TimelineMetadata(), EventPage("""
            [{"type":"damage","timestamp":1000,"sourceID":8,"targetID":7,"abilityGameID":1,
              "resourceActor":2,"hitPoints":40,"maxHitPoints":100},
             {"type":"heal","timestamp":1200,"sourceID":8,"targetID":7,"abilityGameID":3,
              "amount":3000,"resourceActor":2,"hitPoints":70,"maxHitPoints":100}]
            """));
        var service = CreateService(handler);
        var fresh = await service.LoadAsync("estimatedHealth", 1, 7, "Player", "anniversary", default);
        var cached = await service.TryLoadCachedAsync("estimatedHealth", 1, 7, "Player", "anniversary", default);
        Assert.Null(fresh.MaxHealth);
        Assert.Equal(10000, fresh.EstimatedMaxHealth);
        Assert.NotNull(cached);
        Assert.Equal(fresh.EstimatedMaxHealth, cached.EstimatedMaxHealth);
        Assert.Equal(new TimelineHealthCapacity(200, null, 10000), fresh.HealthCapacities[^1]);
        Assert.Equal(fresh.HealthCapacities, cached.HealthCapacities);
        Assert.Equal(fresh.Events, cached.Events);
        Assert.Equal(2, handler.GraphQlRequests);
    }

    [Fact]
    public void NormalizeHealsCalculatesExactBeforeFromAbsoluteEffectiveHealing()
    {
        var events = ParseEvents("""
            [{"type":"heal","timestamp":1000,"sourceID":8,"targetID":7,"abilityGameID":3,
              "amount":3000,"overheal":2000,"resourceActor":2,"hitPoints":10000,"maxHitPoints":10000}]
            """);
        var heal = Assert.Single(ReportTimelineService.NormalizeEvents(events, 7, 1000, 2000, true));
        Assert.Equal(70, heal.HealthBeforePercent);
        Assert.Equal(100, heal.HealthAfterPercent);
        Assert.False(heal.HealthBeforeIsPreviousSnapshot);
    }

    [Fact]
    public void NormalizeHealsPreservesUnknownHealthAndRejectsOtherActorSnapshots()
    {
        var events = ParseEvents("""
            [{"type":"heal","timestamp":1000,"sourceID":8,"targetID":7,"abilityGameID":3,
              "amount":3000,"resourceActor":2,"hitPoints":70,"maxHitPoints":100},
             {"type":"heal","timestamp":1100,"sourceID":8,"targetID":7,"abilityGameID":3,
              "amount":1000,"resourceActor":1,"hitPoints":100,"maxHitPoints":100},
             {"type":"damage","timestamp":1200,"sourceID":8,"targetID":7,"abilityGameID":1},
             {"type":"heal","timestamp":1300,"sourceID":8,"targetID":7,"abilityGameID":3,"amount":500}]
            """);
        var heals = ReportTimelineService.NormalizeEvents(events, 7, 1000, 2000, true)
            .Where(item => item.Kind == "heal").ToArray();
        Assert.Null(heals[0].HealthBeforePercent);
        Assert.Equal(70, heals[0].HealthAfterPercent);
        Assert.Null(heals[1].HealthAfterPercent);
        Assert.Null(heals[2].HealthBeforePercent);
        Assert.Null(heals[2].HealthAfterPercent);
    }

    [Fact]
    public void NormalizeDamageIncludesHealthBeforeAndAfter()
    {
        var events = ParseEvents("""
            [{"type":"cast","timestamp":1000,"sourceID":7,"abilityGameID":1,
              "hitPoints":70,"maxHitPoints":100},
             {"type":"damage","timestamp":1100,"sourceID":8,"targetID":7,"abilityGameID":2,
              "amount":1500,"resourceActor":2,"hitPoints":55,"maxHitPoints":100},
             {"type":"damage","timestamp":1200,"sourceID":8,"targetID":7,"abilityGameID":2,
              "amount":1500,"overkill":-1,"resourceActor":2,"hitPoints":4000,"maxHitPoints":10000}]
            """);
        var damage = ReportTimelineService.NormalizeEvents(events, 7, 1000, 2000, true)
            .Where(item => item.Kind == "damage").ToArray();
        Assert.Equal(70, damage[0].HealthBeforePercent);
        Assert.Equal(55, damage[0].HealthAfterPercent!.Value, 8);
        Assert.True(damage[0].HealthBeforeIsPreviousSnapshot);
        Assert.Equal(55, damage[1].HealthBeforePercent!.Value, 8);
        Assert.Equal(40, damage[1].HealthAfterPercent);
        Assert.False(damage[1].HealthBeforeIsPreviousSnapshot);
    }

    [Fact]
    public void NormalizeCastsGroupsImmediateResultsByTargetAndKindAndExcludesUnrelatedEffects()
    {
        var events = ParseEvents("""
            [{"type":"cast","timestamp":1000,"sourceID":7,"abilityGameID":1},
             {"type":"damage","timestamp":1000,"sourceID":7,"targetID":8,"abilityGameID":1,"amount":500},
             {"type":"damage","timestamp":1100,"sourceID":7,"targetID":8,"abilityGameID":1,"amount":250},
             {"type":"damage","timestamp":1250,"sourceID":7,"targetID":9,"abilityGameID":1,"amount":300},
             {"type":"heal","timestamp":1100,"sourceID":7,"targetID":8,"abilityGameID":1,"amount":100},
             {"type":"damage","timestamp":1251,"sourceID":7,"targetID":9,"abilityGameID":1,"amount":9999},
             {"type":"damage","timestamp":1200,"sourceID":7,"targetID":8,"abilityGameID":1,"amount":9999,"tick":true},
             {"type":"damage","timestamp":1200,"sourceID":6,"targetID":8,"abilityGameID":1,"amount":9999},
             {"type":"damage","timestamp":1200,"sourceID":7,"targetID":8,"abilityGameID":2,"amount":9999},
             {"type":"damage","timestamp":999,"sourceID":7,"targetID":8,"abilityGameID":1,"amount":9999},
             {"type":"cast","timestamp":2000,"sourceID":7,"abilityGameID":1},
             {"type":"damage","timestamp":2050,"sourceID":7,"targetID":8,"abilityGameID":1,"amount":600}]
            """);
        var casts = ReportTimelineService.NormalizeEvents(events, 7, 1000, 3000, true,
                new Dictionary<int, string> { [8] = "Target", [9] = "Other" })
            .Where(item => item.Kind == "cast").ToArray();
        Assert.Equal(new[]
        {
            new TimelineCastResult(8, "Target", "damage", 750),
            new TimelineCastResult(8, "Target", "heal", 100),
            new TimelineCastResult(9, "Other", "damage", 300)
        }, casts[0].CastResults);
        Assert.Equal(new TimelineCastResult(8, "Target", "damage", 600), Assert.Single(casts[1].CastResults!));
    }

    [Fact]
    public void ChainHealIncludesEffectiveBouncesButNotFullyOverhealedTargets()
    {
        var events = ParseEvents("""
            [{"type":"begincast","timestamp":1000,"sourceID":7,"abilityGameID":10623},
             {"type":"cast","timestamp":3385,"sourceID":7,"targetID":8,"abilityGameID":10623},
             {"type":"heal","timestamp":3386,"sourceID":7,"targetID":8,"abilityGameID":10623,"amount":2859,"overheal":419},
             {"type":"heal","timestamp":3386,"sourceID":7,"targetID":9,"abilityGameID":10623,"amount":334,"overheal":498},
             {"type":"heal","timestamp":3386,"sourceID":7,"targetID":10,"abilityGameID":10623,"amount":0,"overheal":1644}]
            """);
        var cast = Assert.Single(ReportTimelineService.NormalizeEvents(events, 7, 0, 10000, true));
        Assert.Equal(new[]
        {
            new TimelineCastResult(8, "Actor 8", "heal", 2859),
            new TimelineCastResult(9, "Actor 9", "heal", 334)
        }, cast.CastResults);
    }

    [Fact]
    public void TimelineMetadataQueriesIncludeNpcAndPetNames()
    {
        foreach (var resource in new[] { "Timeline.Metadata.graphql", "Timeline.ReportMetadata.graphql", "Timeline.MasterData.graphql" })
        {
            var query = GraphQlResourceLoader.Load(resource);
            Assert.Contains("actors { id name type gameID }", query);
            Assert.DoesNotContain("actors(type:", query);
        }
    }

        [Theory]
        [InlineData(251, true)]
        [InlineData(682, true)]
        [InlineData(3000, true)]
        [InlineData(3001, false)]
        public void NormalizeCastsMatchesDelayedProjectileDamageToRecordedTarget(int delay, bool expected)
        {
            var events = ParseEvents($$"""
                [{"type":"begincast","timestamp":1000,"sourceID":7,"abilityGameID":27209},
                 {"type":"cast","timestamp":3385,"sourceID":7,"targetID":98,"abilityGameID":27209},
                 {"type":"damage","timestamp":{{3385 + delay}},"sourceID":7,"targetID":98,"abilityGameID":27209,"amount":3091,"isAoE":false}]
                """);
            var cast = Assert.Single(ReportTimelineService.NormalizeEvents(events, 7, 0, 10000, true,
                new Dictionary<int, string> { [98] = "Zul'jin" }));
            Assert.Equal(1000d, cast.CastStart);
            if (expected)
                Assert.Equal(new TimelineCastResult(98, "Zul'jin", "damage", 3091), Assert.Single(cast.CastResults!));
            else
                Assert.Null(cast.CastResults);
        }

        [Fact]
        public void NormalizeCastsExcludesUnrelatedDelayedHitsAndKeepsSuccessiveProjectilesDistinct()
        {
            var events = ParseEvents("""
                [{"type":"cast","timestamp":1000,"sourceID":7,"targetID":98,"abilityGameID":27209},
                 {"type":"damage","timestamp":1500,"sourceID":7,"targetID":99,"abilityGameID":27209,"amount":999},
                 {"type":"damage","timestamp":1500,"sourceID":8,"targetID":98,"abilityGameID":27209,"amount":999},
                 {"type":"damage","timestamp":1500,"sourceID":7,"targetID":98,"abilityGameID":27210,"amount":999},
                 {"type":"damage","timestamp":1500,"sourceID":7,"targetID":98,"abilityGameID":27209,"amount":999,"tick":true},
                 {"type":"damage","timestamp":1500,"sourceID":7,"targetID":98,"abilityGameID":27209,"amount":999,"isAoE":true},
                 {"type":"damage","timestamp":1682,"sourceID":7,"targetID":98,"abilityGameID":27209,"amount":3091},
                 {"type":"damage","timestamp":1800,"sourceID":7,"targetID":98,"abilityGameID":27209,"amount":999},
                 {"type":"cast","timestamp":2838,"sourceID":7,"targetID":98,"abilityGameID":27209},
                 {"type":"damage","timestamp":3528,"sourceID":7,"targetID":98,"abilityGameID":27209,"amount":6395}]
                """);
            var casts = ReportTimelineService.NormalizeEvents(events, 7, 0, 10000, true);
            Assert.Equal(2, casts.Count);
            Assert.Equal(4090, Assert.Single(casts[0].CastResults!).Amount);
            Assert.Equal(6395, Assert.Single(casts[1].CastResults!).Amount);
        }
    [Fact]
    public void LifeTapLinksItsSeparateManaEffectAndRecordedHealthSnapshotChange()
    {
        var events = ParseEvents("""
            [{"type":"cast","timestamp":1000,"sourceID":7,"abilityGameID":27209,"resourceActor":1,"hitPoints":93,"maxHitPoints":100},
             {"type":"cast","timestamp":1001,"sourceID":7,"abilityGameID":27222,"resourceActor":1,"hitPoints":75,"maxHitPoints":100},
             {"type":"resourcechange","timestamp":1009,"sourceID":7,"targetID":7,"abilityGameID":31818,"resourceChangeType":0,"resourceChange":1907,"waste":7},
             {"type":"resourcechange","timestamp":1010,"sourceID":8,"targetID":7,"abilityGameID":31818,"resourceChangeType":0,"resourceChange":999},
             {"type":"resourcechange","timestamp":1010,"sourceID":7,"targetID":8,"abilityGameID":31818,"resourceChangeType":0,"resourceChange":999},
             {"type":"resourcechange","timestamp":1010,"sourceID":7,"targetID":7,"abilityGameID":99,"resourceChangeType":0,"resourceChange":999},
             {"type":"resourcechange","timestamp":1252,"sourceID":7,"targetID":7,"abilityGameID":31818,"resourceChangeType":0,"resourceChange":999}]
            """);
        var tap = Assert.Single(ReportTimelineService.NormalizeEvents(events, 7, 0, 10000, true),
            item => item.Kind == "cast" && item.SpellId == 27222);
        Assert.Equal(new TimelineCastResult(7, "Actor 7", "mana-returned", 1900), Assert.Single(tap.CastResults!));
        Assert.Equal(93, tap.HealthBeforePercent);
        Assert.Equal(75, tap.HealthAfterPercent);
        Assert.True(tap.HealthBeforeIsPreviousSnapshot);
    }

    [Fact]
    public void LifeTapDoesNotInventMissingHealthOrMatchUnrelatedMana()
    {
        var events = ParseEvents("""
            [{"type":"resourcechange","timestamp":500,"sourceID":7,"targetID":7,"abilityGameID":31818,"resourceChangeType":0,"resourceChange":999},
             {"type":"cast","timestamp":1000,"sourceID":7,"abilityGameID":27222},
             {"type":"resourcechange","timestamp":1001,"sourceID":7,"targetID":7,"abilityGameID":31818,"resourceChangeType":0,"resourceChange":999,"waste":999}]
            """);
        var tap = Assert.Single(ReportTimelineService.NormalizeEvents(events, 7, 0, 10000, true),
            item => item.Kind == "cast");
        Assert.Null(tap.CastResults);
        Assert.Null(tap.HealthBeforePercent);
        Assert.Null(tap.HealthAfterPercent);
    }

    [Fact]
    public void PeriodicDamageTotalsFollowTheLatestCastOnEachRecordedTarget()
    {
        var events = ParseEvents("""
            [{"type":"damage","timestamp":500,"sourceID":7,"targetID":98,"abilityGameID":27216,"amount":999,"tick":true},
             {"type":"cast","timestamp":1000,"sourceID":7,"targetID":98,"abilityGameID":27216},
             {"type":"cast","timestamp":2000,"sourceID":7,"targetID":99,"abilityGameID":27216},
             {"type":"damage","timestamp":4000,"sourceID":7,"targetID":98,"abilityGameID":27216,"amount":600,"tick":true},
             {"type":"damage","timestamp":5000,"sourceID":7,"targetID":99,"abilityGameID":27216,"amount":400,"tick":true},
             {"type":"damage","timestamp":7000,"sourceID":7,"targetID":98,"abilityGameID":27216,"amount":650,"tick":true},
             {"type":"cast","timestamp":8000,"sourceID":7,"targetID":98,"abilityGameID":27216},
             {"type":"damage","timestamp":11000,"sourceID":7,"targetID":98,"abilityGameID":27216,"amount":700,"tick":true},
             {"type":"damage","timestamp":14000,"sourceID":7,"targetID":98,"abilityGameID":27216,"amount":750,"tick":true},
             {"type":"damage","timestamp":14000,"sourceID":8,"targetID":98,"abilityGameID":27216,"amount":999,"tick":true},
             {"type":"damage","timestamp":14000,"sourceID":7,"targetID":100,"abilityGameID":27216,"amount":999,"tick":true},
             {"type":"damage","timestamp":14000,"sourceID":7,"targetID":98,"abilityGameID":27217,"amount":999,"tick":true},
             {"type":"heal","timestamp":14000,"sourceID":7,"targetID":98,"abilityGameID":27216,"amount":999,"tick":true},
             {"type":"damage","timestamp":14000,"sourceID":7,"targetID":98,"abilityGameID":27216,"amount":0,"tick":true},
             {"type":"damage","timestamp":16000,"sourceID":7,"targetID":98,"abilityGameID":27216,"amount":999,"tick":true}]
            """);
        var casts = ReportTimelineService.NormalizeEvents(events, 7, 0, 15000, true,
            new Dictionary<int, string> { [98] = "Zul'jin", [99] = "Other target" });
        Assert.Equal(3, casts.Count);
        Assert.Equal(new TimelineCastResult(98, "Zul'jin", "damage", 1250), Assert.Single(casts[0].CastResults!));
        Assert.Equal(new TimelineCastResult(99, "Other target", "damage", 400), Assert.Single(casts[1].CastResults!));
        Assert.Equal(new TimelineCastResult(98, "Zul'jin", "damage", 1450), Assert.Single(casts[2].CastResults!));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PeriodicDamageDoesNotGuessMissingTargetsOrAmbiguousCasts(bool ambiguous)
    {
        var events = ParseEvents(ambiguous ? """
            [{"type":"cast","timestamp":1000,"sourceID":7,"targetID":98,"abilityGameID":27216},
             {"type":"cast","timestamp":1000,"sourceID":7,"targetID":98,"abilityGameID":27216},
             {"type":"damage","timestamp":4000,"sourceID":7,"targetID":98,"abilityGameID":27216,"amount":600,"tick":true}]
            """ : """
            [{"type":"cast","timestamp":1000,"sourceID":7,"abilityGameID":27216},
             {"type":"damage","timestamp":4000,"sourceID":7,"targetID":98,"abilityGameID":27216,"amount":600,"tick":true}]
            """);
        Assert.All(ReportTimelineService.NormalizeEvents(events, 7, 0, 10000, true),
            cast => Assert.Null(cast.CastResults));
    }

    [Fact]
    public async Task LoadFetchesAndCachesImmediateCastResults()
    {
        using var handler = new QueueHandler(
            TimelineMetadata("""[{"id":7,"name":"Player","type":"Player"},{"id":98,"name":"Zul'jin","type":"NPC"}]"""),
            EventPage("""
                [{"type":"cast","timestamp":2000,"sourceID":7,"abilityGameID":1},
                 {"type":"damage","timestamp":2010,"sourceID":7,"targetID":98,"abilityGameID":1,"amount":1250}]
                """));
        var service = CreateService(handler);
        var fresh = await service.LoadAsync("castResults", 1, 7, "Player", "anniversary", default);
        var cached = await service.TryLoadCachedAsync("castResults", 1, 7, "Player", "anniversary", default);
        var expected = new TimelineCastResult(98, "Zul'jin", "damage", 1250);
        Assert.Equal(expected, Assert.Single(Assert.Single(fresh.Events).CastResults!));
        Assert.NotNull(cached);
        Assert.Equal(expected, Assert.Single(Assert.Single(cached.Events).CastResults!));
        Assert.Equal(2, handler.GraphQlRequests);
    }

    [Fact]
    public async Task LoadIncludesCorruptionTickTotalsFromFreshAndCachedEvents()
    {
        using var handler = new QueueHandler(
            TimelineMetadata("""[{"id":7,"name":"Player","type":"Player"},{"id":98,"name":"Zul'jin","type":"NPC"}]"""),
            EventPage("""
                [{"type":"cast","timestamp":2000,"sourceID":7,"targetID":98,"abilityGameID":27216},
                 {"type":"damage","timestamp":5000,"sourceID":7,"targetID":98,"abilityGameID":27216,"amount":600,"tick":true},
                 {"type":"damage","timestamp":8000,"sourceID":7,"targetID":98,"abilityGameID":27216,"amount":650,"tick":true}]
                """));
        var service = CreateService(handler);
        var fresh = await service.LoadAsync("corruptionTicks", 1, 7, "Player", "anniversary", default);
        var cached = await service.TryLoadCachedAsync("corruptionTicks", 1, 7, "Player", "anniversary", default);
        var expected = new TimelineCastResult(98, "Zul'jin", "damage", 1250);
        Assert.Equal(expected, Assert.Single(Assert.Single(fresh.Events).CastResults!));
        Assert.NotNull(cached);
        Assert.Equal(expected, Assert.Single(Assert.Single(cached.Events).CastResults!));
        Assert.Equal(2, handler.GraphQlRequests);
    }

    [Theory]
    [InlineData("anniversary", 23863, "Zul'jin")]
    [InlineData("anniversary", 23864, "Daakara")]
    [InlineData("retail", 23863, "Daakara")]
    public async Task TimelineActorNamesCorrectOnlyAnniversaryZuljin(string edition, int npcId, string expectedName)
    {
        using var handler = new QueueHandler(
            TimelineMetadata($$"""[{"id":7,"name":"Player","type":"Player"},{"id":98,"name":"Daakara","type":"NPC","gameID":{{npcId}}}]"""),
            EventPage("""
                [{"type":"cast","timestamp":2000,"sourceID":7,"abilityGameID":1},
                 {"type":"damage","timestamp":2010,"sourceID":7,"targetID":98,"abilityGameID":1,"amount":1250}]
                """));
        var service = CreateService(handler);
        var fresh = await service.LoadAsync("zuljinActor", 1, 7, "Player", edition, default);
        Assert.Equal(expectedName, Assert.Single(Assert.Single(fresh.Events).CastResults!).TargetName);
        var cached = await service.TryLoadCachedAsync("zuljinActor", 1, 7, "Player", edition, default);
        Assert.NotNull(cached);
        Assert.Equal(expectedName, Assert.Single(Assert.Single(cached.Events).CastResults!).TargetName);
    }

    [Fact]
    public void NormalizeCastsDoesNotAttachResultsToAmbiguousSimultaneousCasts()
    {
        var events = ParseEvents("""
            [{"type":"cast","timestamp":1000,"sourceID":7,"abilityGameID":1},
             {"type":"cast","timestamp":1000,"sourceID":7,"abilityGameID":1},
             {"type":"damage","timestamp":1000,"sourceID":7,"targetID":8,"abilityGameID":1,"amount":500}]
            """);
        var casts = ReportTimelineService.NormalizeEvents(events, 7, 1000, 2000, true);
        Assert.Equal(2, casts.Count);
        Assert.All(casts, item => Assert.Null(item.CastResults));
    }

    [Fact]
    public void HealthCapacitiesTrackTemporaryRecordedMaximumsWithoutUsingFightPeak()
    {
        var raw = ParseEvents("""
            [{"type":"cast","timestamp":1000,"sourceID":7,"abilityGameID":1,"maxHitPoints":10000,"hitPoints":8000},
             {"type":"applybuff","timestamp":2000,"targetID":7,"abilityGameID":2},
             {"type":"cast","timestamp":2100,"sourceID":7,"abilityGameID":1,"maxHitPoints":13000,"hitPoints":10000},
             {"type":"removebuff","timestamp":3000,"targetID":7,"abilityGameID":2},
             {"type":"cast","timestamp":3100,"sourceID":7,"abilityGameID":1,"maxHitPoints":10000,"hitPoints":8000},
             {"type":"cast","timestamp":4001,"sourceID":7,"abilityGameID":1,"maxHitPoints":99999,"hitPoints":8000}]
            """);
        var events = ReportTimelineService.NormalizeEvents(raw, 7, 1000, 4000, true);
        Assert.Equal(new[]
        {
            new TimelineHealthCapacity(0, 10000, null),
            new TimelineHealthCapacity(1000, null, null),
            new TimelineHealthCapacity(1100, 13000, null),
            new TimelineHealthCapacity(2000, null, null),
            new TimelineHealthCapacity(2100, 10000, null)
        }, ReportTimelineService.HealthCapacities(raw, events, 7, 1000, 4000));
    }

    [Fact]
    public void HealthCapacitiesUseLocalEstimatesAndRejectHealsAcrossAuraBoundaries()
    {
        var raw = ParseEvents("""
            [{"type":"cast","timestamp":1000,"sourceID":7,"abilityGameID":1,"maxHitPoints":100,"hitPoints":40},
             {"type":"heal","timestamp":1100,"sourceID":8,"targetID":7,"abilityGameID":3,"amount":3000,
              "maxHitPoints":100,"hitPoints":70},
             {"type":"applybuff","timestamp":2000,"targetID":7,"abilityGameID":2},
             {"type":"heal","timestamp":2100,"sourceID":8,"targetID":7,"abilityGameID":3,"amount":6000,
              "maxHitPoints":100,"hitPoints":90},
             {"type":"damage","timestamp":2200,"sourceID":8,"targetID":7,"abilityGameID":4,
              "maxHitPoints":100,"hitPoints":40},
             {"type":"heal","timestamp":2300,"sourceID":8,"targetID":7,"abilityGameID":3,"amount":6000,
              "maxHitPoints":100,"hitPoints":70},
             {"type":"removebuff","timestamp":3000,"targetID":7,"abilityGameID":2}]
            """);
        var events = ReportTimelineService.NormalizeEvents(raw, 7, 1000, 4000, true);
        Assert.Null(events.Single(item => item.Kind == "heal" && item.Timestamp == 1100).HealthBeforePercent);
        Assert.Equal(new[]
        {
            new TimelineHealthCapacity(0, null, null),
            new TimelineHealthCapacity(100, null, 10000),
            new TimelineHealthCapacity(1000, null, null),
            new TimelineHealthCapacity(1300, null, 20000),
            new TimelineHealthCapacity(2000, null, null)
        }, ReportTimelineService.HealthCapacities(raw, events, 7, 1000, 4000));
    }

    [Fact]
    public void PercentageHealsDoNotEstimateFromStaleSnapshots()
    {
        var raw = ParseEvents("""
            [{"type":"cast","timestamp":1000,"sourceID":7,"abilityGameID":1,"hitPoints":40,"maxHitPoints":100},
             {"type":"heal","timestamp":2001,"sourceID":8,"targetID":7,"abilityGameID":2,"amount":3000,
              "hitPoints":70,"maxHitPoints":100}]
            """);
        var events = ReportTimelineService.NormalizeEvents(raw, 7, 1000, 4000, true);
        Assert.Null(events.Single(item => item.Kind == "heal").HealthBeforePercent);
        Assert.Null(ReportTimelineService.EstimateMaxHealth(events));
        Assert.All(ReportTimelineService.HealthCapacities(raw, events, 7, 1000, 4000),
            item => Assert.Null(item.EstimatedMaxHealth));
    }

    [Fact]
    public void NonHealthAurasDoNotInvalidateLocalPercentageEstimates()
    {
        var raw = ParseEvents("""
            [{"type":"resourcechange","timestamp":1000,"sourceID":8,"targetID":7,"resourceActor":2,
              "hitPoints":65,"maxHitPoints":100},
             {"type":"applybuff","timestamp":1927,"targetID":7,"abilityGameID":24907},
             {"type":"heal","timestamp":1956,"sourceID":7,"targetID":7,"resourceActor":1,
              "abilityGameID":25420,"amount":3240,"hitPoints":100,"maxHitPoints":100},
             {"type":"refreshbuff","timestamp":2000,"targetID":7,"abilityGameID":33736},
             {"type":"applybuff","timestamp":2001,"targetID":7,"abilityGameID":16237}]
            """);
        var events = ReportTimelineService.NormalizeEvents(raw, 7, 1000, 3000, true);
        var heal = Assert.Single(events);
        Assert.Equal(65, heal.HealthBeforePercent);
        Assert.Equal(100, heal.HealthAfterPercent);
        Assert.Equal(new[]
        {
            new TimelineHealthCapacity(0, null, null),
            new TimelineHealthCapacity(956, null, 9257)
        }, ReportTimelineService.HealthCapacities(raw, events, 7, 1000, 3000));
    }

    [Fact]
    public void EventQueryRequestsResourceSnapshots()
    {
        Assert.Contains("includeResources: true", GraphQlResourceLoader.Load("Timeline.Events.graphql"));
    }

    [Fact]
    public async Task LoadFetchesAndCachesRecordedResourceMaximums()
    {
        using var handler = new QueueHandler(TimelineMetadata(), EventPage("""
            [{"type":"cast","timestamp":2000,"sourceID":7,"resourceActor":1,"abilityGameID":1,
              "maxHitPoints":12000,"classResources":[{"type":0,"max":7173}]}]
            """));
        var service = CreateService(handler);

        var fresh = await service.LoadAsync("resourceMaximums", 1, 7, "Player", "anniversary", default);
        var cached = await service.TryLoadCachedAsync("resourceMaximums", 1, 7, "Player", "anniversary", default);

        Assert.Equal(12000, fresh.MaxHealth);
        Assert.Equal(7173, fresh.MaxMana);
        Assert.NotNull(cached);
        Assert.Equal(fresh.MaxHealth, cached.MaxHealth);
        Assert.Equal(new TimelineHealthCapacity(1000, 12000, null), Assert.Single(fresh.HealthCapacities));
        Assert.Equal(fresh.HealthCapacities, cached.HealthCapacities);
        Assert.Equal(fresh.MaxMana, cached.MaxMana);
        Assert.Equal(2, handler.GraphQlRequests);
    }

    [Fact]
    public void ResourceMaximumDoesNotTreatPercentageHealthAsAbsoluteHitPoints()
    {
        var events = ParseEvents("""
            [{"type":"damage","timestamp":2000,"sourceID":8,"targetID":7,"resourceActor":2,
              "hitPoints":99,"maxHitPoints":100,"classResources":[{"type":0,"max":7173}]}]
            """);
        Assert.Null(ReportTimelineService.ResourceMaximum(events, 7, false));
        Assert.Equal(7173, ReportTimelineService.ResourceMaximum(events, 7, true));
    }

    [Fact]
    public void ResourceMaximumHonorsSnapshotOwnerAndSelectedFight()
    {
        var events = ParseEvents("""
            [{"type":"damage","timestamp":1000,"sourceID":7,"targetID":8,"resourceActor":1,
              "maxHitPoints":12000,"classResources":[{"type":0,"max":5500}]},
             {"type":"cast","timestamp":2000,"sourceID":8,"targetID":7,"resourceActor":2,
              "maxHitPoints":13000,"classResources":[{"type":0,"max":6000}]},
             {"type":"damage","timestamp":2500,"sourceID":8,"targetID":7,"resourceActor":1,
              "maxHitPoints":999999,"classResources":[{"type":0,"max":999999}]},
             {"type":"cast","timestamp":3000,"sourceID":7,"resourceActor":3,"maxHitPoints":999999},
             {"type":"cast","timestamp":999,"sourceID":7,"maxHitPoints":999999},
             {"type":"cast","timestamp":3001,"sourceID":7,"maxHitPoints":999999}]
            """);
        Assert.Equal(13000, ReportTimelineService.ResourceMaximum(events, 7, false, 1000, 3000));
        Assert.Equal(6000, ReportTimelineService.ResourceMaximum(events, 7, true, 1000, 3000));
    }

    [Fact]
    public void ResourceMaximumUsesOnlySelectedActorSnapshotsAndPreservesUnknown()
    {
        var events = ParseEvents("""
            [{"type":"cast","sourceID":7,"targetID":8,"maxHitPoints":10000,
              "classResources":[{"type":0,"max":5000}]},
             {"type":"heal","sourceID":8,"targetID":7,"maxHitPoints":12000,
              "classResources":[{"type":0,"max":5500}]},
             {"type":"damage","sourceID":7,"targetID":8,"maxHitPoints":999999,
              "classResources":[{"type":0,"max":999999}]}]
            """);
        Assert.Equal(12000, ReportTimelineService.ResourceMaximum(events, 7, false));
        Assert.Equal(5500, ReportTimelineService.ResourceMaximum(events, 7, true));
        Assert.Null(ReportTimelineService.ResourceMaximum(events, 9, false));
        Assert.Null(ReportTimelineService.ResourceMaximum(events, 9, true));
    }

    [Fact]
    public void NormalizeEventsAttributesIncomingEventsToTheirSourceActors()
    {
        var events = ParseEvents("""
            [{"type":"heal","timestamp":1000,"sourceID":8,"targetID":7,"abilityGameID":1,"amount":250},
             {"type":"damage","timestamp":2000,"sourceID":9,"targetID":7,"abilityGameID":2,"amount":500}]
            """);
        var result = ReportTimelineService.NormalizeEvents(events, 7, 0, 10000, true,
            new Dictionary<int, string> { [8] = "Healer", [9] = "Boss" });
        Assert.Equal("Healer", result[0].SourceName);
        Assert.Equal("Boss", result[1].SourceName);
    }

    [Fact]
    public void NormalizeEventsUsesRecordedPotionExpiryAfterFightWithoutExtendingUnknownBuffs()
    {
        var events = ParseEvents("""
            [{"type":"applybuff","timestamp":1000,"targetID":7,"abilityGameID":28515},
             {"type":"applybuff","timestamp":2000,"targetID":7,"abilityGameID":99},
             {"type":"removebuff","timestamp":121014,"targetID":7,"abilityGameID":28515}]
            """);
        var result = ReportTimelineService.NormalizeEvents(events, 7, 0, 100000, true);
        Assert.Equal(new TimelineEvent(28515, 1000, 99000, "buff"), Assert.Single(result));
    }

    [Fact]
    public void NormalizeEventsDoesNotExtendUnclosedPotionBuffsButPreservesRecordedUse()
    {
        var events = ParseEvents("""
            [{"type":"cast","timestamp":1000,"sourceID":7,"abilityGameID":28515},
             {"type":"applybuff","timestamp":1000,"targetID":7,"abilityGameID":28515},
             {"type":"refreshbuff","timestamp":10000,"targetID":7,"abilityGameID":28515}]
            """);
        var result = ReportTimelineService.NormalizeEvents(events, 7, 0, 300000, true);
        Assert.Equal(new TimelineEvent(28515, 1000, null, "cast"), Assert.Single(result));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PostFightReapplicationDoesNotPairItsRemovalWithAnEarlierUnclosedBuff(bool complete)
    {
        var events = ParseEvents("""
            [{"type":"applybuff","timestamp":1000,"targetID":7,"abilityGameID":99},
             {"type":"applybuff","timestamp":101000,"targetID":7,"abilityGameID":99},
             {"type":"removebuff","timestamp":110000,"targetID":7,"abilityGameID":99}]
            """);
        Assert.Empty(ReportTimelineService.NormalizeEvents(events, 7, 0, 100000, complete));
    }

    [Fact]
    public void PostFightReapplicationDoesNotSuppressAnAlreadyMeasuredInFightInterval()
    {
        var events = ParseEvents("""
            [{"type":"applybuff","timestamp":1000,"targetID":7,"abilityGameID":99},
             {"type":"removebuff","timestamp":90000,"targetID":7,"abilityGameID":99},
             {"type":"applybuff","timestamp":101000,"targetID":7,"abilityGameID":99},
             {"type":"removebuff","timestamp":110000,"targetID":7,"abilityGameID":99}]
            """);
        Assert.Equal(new TimelineEvent(99, 1000, 89000, "buff"),
            Assert.Single(ReportTimelineService.NormalizeEvents(events, 7, 0, 100000, true)));
    }

    [Fact]
    public void NormalizeEventsMeasuresMatchedCastWindowsWithoutGuessingUnmatchedCasts()
    {
        var events = ParseEvents("""
            [{"type":"begincast","sourceID":7,"abilityGameID":1,"timestamp":1000},
             {"type":"cast","sourceID":7,"abilityGameID":1,"timestamp":3500},
             {"type":"begincast","sourceID":7,"abilityGameID":2,"timestamp":4000},
             {"type":"interrupt","targetID":7,"timestamp":4500},
             {"type":"cast","sourceID":7,"abilityGameID":2,"timestamp":5000},
             {"type":"cast","sourceID":7,"abilityGameID":3,"timestamp":6000}]
            """);
        var casts = ReportTimelineService.NormalizeEvents(events, 7, 0, 10000, true);
        Assert.Equal(3, casts.Count);
        Assert.Equal(1000d, casts[0].CastStart);
        Assert.Equal(2500d, casts[0].Duration);
        Assert.Equal(3500d, casts[0].Timestamp);
        Assert.Null(casts[1].CastStart);
        Assert.Null(casts[1].Duration);
        Assert.Null(casts[2].Duration);
    }

    [Fact]
    public void NormalizeEventsIncludesOnlyReceivedHealingAndDistinguishesTicks()
    {
        var events = ParseEvents("""
            [{"type":"heal","timestamp":2000,"sourceID":8,"targetID":7,"abilityGameID":1},
             {"type":"heal","timestamp":3000,"sourceID":8,"targetID":7,"abilityGameID":2,"tick":true},
             {"type":"heal","timestamp":4000,"sourceID":7,"targetID":8,"abilityGameID":3},
             {"type":"heal","timestamp":12000,"targetID":7,"abilityGameID":4},
             {"type":"damage","timestamp":5000,"sourceID":8,"targetID":7,"abilityGameID":5},
             {"type":"damage","timestamp":6000,"sourceID":7,"targetID":8,"abilityGameID":6}]
            """);
        var result = ReportTimelineService.NormalizeEvents(events, 7, 1000, 11000, true);
        Assert.Equal(new[] {
            new TimelineEvent(1, 1000, null, "heal"),
            new TimelineEvent(2, 2000, null, "hot"),
            new TimelineEvent(5, 4000, null, "damage")
        }, result);
    }

    [Fact]
    public void NormalizeEventsTracksRecordedManaCostsReturnsAndDrainsForSelectedActor()
    {
        var events = ParseEvents("""
            [{"type":"cast","timestamp":2000,"sourceID":7,"targetID":8,"abilityGameID":1,
              "classResources":[{"type":0,"cost":250},{"type":1,"cost":10}]},
             {"type":"cast","timestamp":2500,"sourceID":8,"abilityGameID":2,
              "classResources":[{"type":0,"cost":999}]},
             {"type":"resourcechange","timestamp":3000,"sourceID":8,"targetID":7,"abilityGameID":3,
              "resourceChangeType":0,"resourceChange":600,"waste":100},
             {"type":"resourcechange","timestamp":4000,"targetID":7,"abilityGameID":4,
              "resourceChangeType":1,"resourceChange":50},
             {"type":"resourcechange","timestamp":5000,"targetID":8,"abilityGameID":5,
              "resourceChangeType":0,"resourceChange":999},
             {"type":"resourcechange","timestamp":6000,"targetID":7,"abilityGameID":6,
              "resourceChangeType":0,"resourceChange":100,"waste":100},
             {"type":"drain","timestamp":7000,"targetID":7,"abilityGameID":7,
              "resourceChangeType":0,"resourceChange":125}]
            """);
        var result = ReportTimelineService.NormalizeEvents(events, 7, 1000, 11000, true);
        Assert.Equal(new[] {
            new TimelineEvent(1, 1000, null, "cast"),
            new TimelineEvent(1, 1000, null, "mana-spent", Amount: 250),
            new TimelineEvent(3, 2000, null, "mana-returned", Amount: 500),
            new TimelineEvent(7, 6000, null, "mana-spent", Amount: 125, IsManaDrain: true)
        }, result);
    }

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

        Assert.Equal(3, result.Count);
        Assert.Equal(new TimelineEvent(1, 1000, null, "cast"), result[0]);
        Assert.Equal(new TimelineEvent(2, 2000, 2000, "buff"), result[1]);
        Assert.Equal(new TimelineEvent(0, 5000, null, "death"), result[2]);
        Assert.DoesNotContain(result, item => item.SpellId == 4);
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
    public async Task IncompleteMetadataIsNotCachedAndLiveLoadRecovers()
    {
        const string emptyMetadata = """
            {"data":{"reportData":{"report":{"code":"verified-report","fights":[],
            "masterData":{"actors":null,"abilities":null}}}}}
            """;
        using var handler = new QueueHandler(emptyMetadata, TimelineMetadata(), EventPage("[]"));
        var client = fixture.CreateClient(handler);
        await client.QueryGraphQLAsync(GraphQlResourceLoader.Load("Timeline.Metadata.graphql"),
            new { code = "report", fightIds = new[] { 1 } }, "anniversary");
        var logger = new RecordingLogger();
        var service = new ReportTimelineService(client, logger);

        Assert.Null(await service.TryLoadCachedAsync("report", 1, 7, "Player", "anniversary", default));
        Assert.Equal(1, handler.GraphQlRequests);

        var timeline = await service.LoadAsync("report", 1, 7, "Player", "anniversary", default);

        Assert.Equal(1, timeline.FightId);
        Assert.Equal("Player", timeline.PlayerName);
        Assert.Equal(3, handler.GraphQlRequests);
    }

    [Fact]
    public async Task LiveLoadStillRejectsMissingFightAfterOneMetadataRefresh()
    {
        using var handler = new QueueHandler(TimelineMetadata(fightId: 2), TimelineMetadata(fightId: 2));
        var service = CreateService(handler);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.LoadAsync("report", 1, 7, null, "anniversary", default));

        Assert.Equal(2, handler.GraphQlRequests);
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
            TimelineMetadata(fightId: id, end: end),
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

    [Theory]
    [InlineData(3470, "Boss")]
    [InlineData(101194, "Zul'jin")]
    public async Task BossLoadFiltersEventsAndPhasesAndKeepsEditionSpecificObservedPhases(int encounterId, string expectedName)
    {
        const string metadata = """
            {"data":{"reportData":{"report":{"fights":[{"id":1,"startTime":1000,"endTime":11000,
            "encounterID":3470,"enemyNPCs":[{"id":10}],"phaseTransitions":[{"id":3,"startTime":7000},
            {"id":2,"startTime":999},{"id":4,"startTime":11001}]}],"masterData":{"abilities":[],"actors":[]}}}}}
            """;
        var encounterMetadata = metadata.Replace("\"encounterID\":3470",
            $"\"name\":\"Daakara\",\"encounterID\":{encounterId}", StringComparison.Ordinal);
        if (encounterId == 3470)
            encounterMetadata = metadata;
        using var handler = new QueueHandler(encounterMetadata, EventPage("""
            [{"type":"applybuff","timestamp":2000,"abilityGameID":1},
             {"type":"cast","timestamp":2000,"abilityGameID":0},
             {"type":"cast","timestamp":999,"abilityGameID":1},
             {"type":"cast","timestamp":11001,"abilityGameID":1},
             {"type":"cast","sourceID":10,"timestamp":5000,"abilityGameID":1}]
            """));

        var result = await CreateService(handler).LoadBossAsync("report", 1, "anniversary", false, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(expectedName, result.Name);
        Assert.Equal(new TimelinePhase(3, 6000), Assert.Single(result.Phases));
        Assert.Equal(new TimelineEvent(1, 4000, null, "cast"), Assert.Single(result.Events));
        Assert.Equal(new TimelineSpell(1, "Spell 1", null), Assert.Single(result.Spells));
    }

    [Fact]
    public async Task BossMeleeKeepsRecordedDamageAvoidanceAndMissingResultsDistinct()
    {
        const string metadata = """
            {"data":{"reportData":{"report":{"fights":[{"id":1,"name":"Boss",
              "startTime":1000,"endTime":11000,"enemyNPCs":[{"id":10}],"phaseTransitions":[]}],
              "masterData":{"abilities":[],"actors":[{"id":7,"name":"Tank"}]}}}}}
            """;
        using var handler = new QueueHandler(metadata, EventPage("""
            [{"type":"cast","sourceID":10,"targetID":7,"timestamp":2000,"abilityGameID":1},
             {"type":"damage","sourceID":10,"targetID":7,"timestamp":2000,"abilityGameID":1,"amount":3343},
             {"type":"cast","sourceID":10,"targetID":7,"timestamp":3500,"abilityGameID":1},
             {"type":"miss","sourceID":10,"targetID":7,"timestamp":3500,"abilityGameID":1},
             {"type":"cast","sourceID":10,"targetID":7,"timestamp":5000,"abilityGameID":1}]
            """));
        var result = await CreateService(handler).LoadBossAsync("report", 1, "anniversary", false, default);
        Assert.NotNull(result);
        Assert.Equal(3, result.Events.Count);
        Assert.Equal(new TimelineCastResult(7, "Tank", "damage", 3343),
            Assert.Single(result.Events[0].CastResults!));
        Assert.Equal(new TimelineCastResult(7, "Tank", "damage", 0, "Missed / avoided"),
            Assert.Single(result.Events[1].CastResults!));
        Assert.Null(result.Events[2].CastResults);
    }

    [Fact]
    public async Task BossLoadIncludesRecordedCastDamageForEachEnemyAndTarget()
    {
        const string metadata = """
            {"data":{"reportData":{"report":{"fights":[{"id":1,"name":"Boss",
              "startTime":1000,"endTime":11000,"enemyNPCs":[{"id":10},{"id":11}],"phaseTransitions":[]}],
              "masterData":{"abilities":[],"actors":[{"id":7,"name":"Player"}]}}}}}
            """;
        using var handler = new QueueHandler(metadata, EventPage("""
            [{"type":"cast","sourceID":10,"targetID":7,"timestamp":2000,"abilityGameID":1},
             {"type":"damage","sourceID":10,"targetID":7,"timestamp":2000,"abilityGameID":1,"amount":1250},
             {"type":"cast","sourceID":11,"targetID":7,"timestamp":2000,"abilityGameID":1},
             {"type":"damage","sourceID":11,"targetID":7,"timestamp":2000,"abilityGameID":1,"amount":500},
             {"type":"cast","sourceID":7,"targetID":10,"timestamp":2000,"abilityGameID":1},
             {"type":"damage","sourceID":7,"targetID":10,"timestamp":2000,"abilityGameID":1,"amount":9999},
             {"type":"applybuff","sourceID":10,"targetID":10,"timestamp":3000,"abilityGameID":2},
             {"type":"removebuff","sourceID":10,"targetID":10,"timestamp":4000,"abilityGameID":2},
             {"type":"heal","sourceID":11,"targetID":10,"timestamp":5000,"abilityGameID":3,"amount":250},
             {"type":"heal","sourceID":11,"targetID":10,"timestamp":6000,"abilityGameID":3,"amount":50,"tick":true},
             {"type":"resourcechange","sourceID":11,"targetID":10,"timestamp":7000,"abilityGameID":4,
              "resourceChangeType":0,"resourceChange":100,"waste":20},
             {"type":"applybuff","sourceID":10,"targetID":10,"timestamp":8000,"abilityGameID":5}]
            """));
        var result = await CreateService(handler).LoadBossAsync("report", 1, "anniversary", false, CancellationToken.None);
        Assert.NotNull(result);
        var casts = result.Events.Where(item => item.Kind == "cast").ToArray();
        Assert.Equal(2, casts.Length);
        Assert.Equal(new TimelineCastResult(7, "Player", "damage", 1250),
            Assert.Single(casts[0].CastResults!));
        Assert.Equal(new TimelineCastResult(7, "Player", "damage", 500),
            Assert.Single(casts[1].CastResults!));
        Assert.Equal(9999, Assert.Single(result.Events.Where(item => item.Kind == "damage")).Amount);
        Assert.Equal(1000, Assert.Single(result.Events.Where(item => item.Kind == "buff" && item.SpellId == 2)).Duration);
        Assert.Equal(3000, Assert.Single(result.Events.Where(item => item.Kind == "buff" && item.SpellId == 5)).Duration);
        Assert.Equal(250, Assert.Single(result.Events.Where(item => item.Kind == "heal")).Amount);
        Assert.Equal(50, Assert.Single(result.Events.Where(item => item.Kind == "hot")).Amount);
        Assert.Equal(80, Assert.Single(result.Events.Where(item => item.Kind == "mana-returned")).Amount);
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
        Assert.Single(fresh.Events);
        Assert.NotNull(cached);
        Assert.Equal(fresh.Events, cached.Events);
        Assert.Equal(2, handler.GraphQlRequests);
    }

    [Fact]
    public async Task LoadBossAppliesIdenticalFreshAndCachedPhasesWithRelativeTimesAndNoteAnchors()
    {
        const string metadata = """
            {"data":{"reportData":{"report":{"fights":[{"id":88,"name":"Nek'zali the Soulcoiler",
            "encounterID":3470,"enemyNPCs":[{"id":10}],
            "startTime":10000,"endTime":110000,"phaseTransitions":[
            {"id":2,"startTime":30000},{"id":3,"startTime":100000}]}],
            "masterData":{"abilities":[],"actors":[]}}}}}
            """;
        const string events = """
            {"data":{"reportData":{"report":{"events":{"data":[
            {"type":"cast","sourceID":10,"timestamp":30000,"abilityGameID":1295124},
            {"type":"cast","sourceID":10,"timestamp":85000,"abilityGameID":1292248}
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

    private sealed class RecordingLogger : ILogger<ReportTimelineService>
    {
        public List<string> Messages { get; } = [];
        public List<Dictionary<string, object?>> Scopes { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> values)
                Scopes.Add(values.ToDictionary());
            return null;
        }
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }

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
                        code = "verified-report",
                        title = "Verified report",
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
