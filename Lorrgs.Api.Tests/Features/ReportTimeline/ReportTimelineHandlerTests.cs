using System.Net;
using System.Text.Json;
using Lorrgs.Api.Features.ReportTimeline;
using Lorrgs.Api.Features.ReportTimeline.GetBossTimeline;
using Lorrgs.Api.Features.ReportTimeline.GetPlayerTimeline;
using Lorrgs.WarcraftLogs;
using Lorrgs.WarcraftLogs.Models;
using Lorrgs.WarcraftLogs.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lorrgs.Api.Tests;

public sealed class ReportTimelineHandlerTests
{
    [Theory]
    [InlineData("", 1, 7, "Player", "retail")]
    [InlineData("bad-code", 1, 7, "Player", "retail")]
    [InlineData("report\r\nforged", 1, 7, "Player", "retail")]
    [InlineData("report\u0085forged", 1, 7, "Player", "retail")]
    [InlineData("report\u2028forged", 1, 7, "Player", "retail")]
    [InlineData("report\u2029forged", 1, 7, "Player", "retail")]
    [InlineData("abcdefghijklmnopqrstuvwxyz1234567", 1, 7, "Player", "retail")]
    [InlineData("report", 0, 7, "Player", "retail")]
    [InlineData("report", 1, null, null, "retail")]
    [InlineData("report", 1, 0, " ", "retail")]
    [InlineData("report", 1, 7, "Player", "invalid")]
    public async Task GetRejectsInvalidRequestsWithoutCallingService(
        string report, int fight, int? actor, string? name, string edition)
    {
        var service = new TimelineService();
        var result = await Handlers(service).Player.Handle(new GetPlayerTimelineRequest(report, fight, actor, name, edition));
        Assert.Equal(400, HttpResultAssertions.Read(result).StatusCode);
        Assert.Equal(0, service.Calls);
    }

    [Theory]
    [InlineData("retail", true)]
    [InlineData("anniversary", false)]
    [InlineData("mistsofpandaria", false)]
    [InlineData("era", false)]
    public async Task GetForwardsPlayerSelectionSpecAndCancellation(string edition, bool useSourceId)
    {
        var service = new TimelineService();
        using var cancellation = new CancellationTokenSource();
        var result = await Handlers(service).Player.Handle(new GetPlayerTimelineRequest("report", 3, useSourceId ? 7 : null, useSourceId ? null : "Player", edition, 264), cancellation.Token);
        Assert.Same(service.Player, HttpResultAssertions.Status(200, result).Value);
        Assert.Equal(("report", 3, useSourceId ? (int?)7 : null, useSourceId ? null : "Player", edition, (int?)264),
            service.PlayerRequest);
        Assert.Equal(cancellation.Token, service.Cancellation);
        Assert.Equal(1, service.Calls);
    }

    [Fact]
    public async Task GetBossLoadsUncachedReferenceWithoutRequiringPlayerSelection()
    {
        var service = new TimelineService();
        using var cancellation = new CancellationTokenSource();
        var result = await Handlers(service).Boss.Handle(new GetBossTimelineRequest("report", 3, "retail"), cancellation.Token);
        Assert.Same(service.Boss, HttpResultAssertions.Status(200, result).Value);
        Assert.Equal(("report", 3, "retail", false), service.BossRequest);
        Assert.Equal(cancellation.Token, service.Cancellation);
    }

    [Fact]
    public async Task GetBossRejectsInvalidRequestsWithoutCallingService()
    {
        var service = new TimelineService();
        var result = await Handlers(service).Boss.Handle(new GetBossTimelineRequest("report", 0));
        Assert.Equal(400, HttpResultAssertions.Read(result).StatusCode);
        Assert.Equal(0, service.Calls);
    }

    public static TheoryData<Exception, int> UpstreamFailures => new()
    {
        { new KeyNotFoundException("Player missing"), 404 },
        { new HttpRequestException("rate limited", null, HttpStatusCode.TooManyRequests), 429 },
        { new HttpRequestException("upstream"), 502 },
        { new InvalidOperationException("metadata"), 502 },
        { new JsonException("malformed"), 502 },
        { new WarcraftLogsReportPrivateException("private"), 403 },
        { new WarcraftLogsReportNotFoundException("missing"), 404 }
    };

    [Theory]
    [MemberData(nameof(UpstreamFailures))]
    public async Task BothEndpointsMapUpstreamFailuresToProblemDetails(Exception failure, int status)
    {
        var handlers = Handlers(new TimelineService { Failure = failure });
        var results = new[] { await handlers.Player.Handle(new GetPlayerTimelineRequest("report", 1, 7, null)), await handlers.Boss.Handle(new GetBossTimelineRequest("report", 1)) };
        foreach (var result in results)
        {
            var response = HttpResultAssertions.Read(result);
            Assert.Equal(status, response.StatusCode);
            Assert.Equal(status, Assert.IsType<ProblemDetails>(response.Value).Status);
        }
    }

    [Fact]
    public async Task ArchivedReportsReturnActionableSubscriberAccessErrorForBothTimelines()
    {
        var handlers = Handlers(new TimelineService
        {
            Failure = new WarcraftLogsReportPrivateException("This report has been archived.")
        });
        var results = new[]
        {
            await handlers.Player.Handle(new GetPlayerTimelineRequest("report", 22, null, "Markofmir", "era")),
            await handlers.Boss.Handle(new GetBossTimelineRequest("report", 22, "era"))
        };
        foreach (var result in results)
        {
            var problem = Assert.IsType<ProblemDetails>(HttpResultAssertions.Status(403, result).Value);
            Assert.Contains("archived", problem.Detail);
            Assert.Contains("subscriber access", problem.Detail);
            Assert.DoesNotContain("Make it public", problem.Detail);
        }
    }

    [Fact]
    public async Task GetBossReturnsBadGatewayWhenReferenceCannotBeLoaded()
    {
        var result = await Handlers(new TimelineService { Boss = null }).Boss.Handle(new GetBossTimelineRequest("report", 1));
        Assert.Equal(502, HttpResultAssertions.Read(result).StatusCode);
    }

    [Fact]
    public async Task CancellationIsNotConvertedIntoAnUpstreamError()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var handlers = Handlers(new TimelineService { Failure = new OperationCanceledException(cancellation.Token) });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handlers.Player.Handle(new GetPlayerTimelineRequest("report", 1, 7, null), cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handlers.Boss.Handle(new GetBossTimelineRequest("report", 1), cancellation.Token));
    }

    [Theory]
    [InlineData(true, 403, "private")]
    [InlineData(false, 404, "not found")]
    public async Task GetReturnsActionableProblemDetailsForUnavailableReports(
        bool isPrivate, int expectedStatus, string expectedDetail)
    {
        Exception failure = isPrivate
            ? new WarcraftLogsReportPrivateException("private")
            : new WarcraftLogsReportNotFoundException("missing");
        var service = new TimelineService { Failure = failure };
        var handlers = Handlers(service);

        var result = await handlers.Player.Handle(new GetPlayerTimelineRequest("unavailable", 1, null, "Player", "retail"));

        var response = HttpResultAssertions.Read(result);
        Assert.Equal(expectedStatus, response.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(response.Value);
        Assert.Equal(expectedStatus, problem.Status);
        Assert.Contains(expectedDetail, problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    private static (GetPlayerTimelineHandler Player, GetBossTimelineHandler Boss) Handlers(IReportTimelineService service)
    {
        var loader = new TimelineLoader(service, NullLogger<TimelineLoader>.Instance);
        return (new(loader), new(loader));
    }

    private sealed class TimelineService : IReportTimelineService
    {
        public Task PopulateRosterTimelinesAsync(RankingReport report, string edition, bool refresh, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Exception? Failure { get; init; }
        public ReportTimeline Player { get; } = new("report", 3, 7, "Player", 60000, true, [], []);
        public BossTimeline? Boss { get; init; } = new("report", 3, "Boss", 60000, true, [], [], []);
        public int Calls { get; private set; }
        public CancellationToken Cancellation { get; private set; }
        public (string, int, int?, string?, string, int?)? PlayerRequest { get; private set; }
        public (string, int, string, bool)? BossRequest { get; private set; }

        public Task<ReportTimeline> LoadAsync(string reportCode, int fightId, int? sourceId, string? playerName,
            string edition, CancellationToken cancellationToken, int? specId = null)
        {
            Calls++;
            Cancellation = cancellationToken;
            PlayerRequest = (reportCode, fightId, sourceId, playerName, edition, specId);
            return Failure is null ? Task.FromResult(Player) : Task.FromException<ReportTimeline>(Failure);
        }

        public Task<BossTimeline?> LoadBossAsync(string reportCode, int fightId, string edition, bool cacheOnly,
            CancellationToken cancellationToken)
        {
            Calls++;
            Cancellation = cancellationToken;
            BossRequest = (reportCode, fightId, edition, cacheOnly);
            return Failure is null ? Task.FromResult(Boss) : Task.FromException<BossTimeline?>(Failure);
        }

        public Task<ReportTimeline?> TryLoadCachedAsync(string reportCode, int fightId, int? sourceId, string? playerName,
            string edition, CancellationToken cancellationToken, int? specId = null) =>
            throw new NotSupportedException();

        public Task PopulateFightPhasesAsync(IReadOnlyList<RankingReport> reports, string edition,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
