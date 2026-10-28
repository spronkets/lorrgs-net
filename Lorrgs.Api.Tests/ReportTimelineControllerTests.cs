using Lorrgs.Api.Controllers;
using Lorrgs.Api.Models;
using Lorrgs.Api.Services;
using Lorrgs.WarcraftLogs;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lorrgs.Api.Tests;

public sealed class ReportTimelineControllerTests
{
    [Theory]
    [InlineData("", 1, 7, "Player", "retail")]
    [InlineData("bad-code", 1, 7, "Player", "retail")]
    [InlineData("abcdefghijklmnopqrstuvwxyz1234567", 1, 7, "Player", "retail")]
    [InlineData("report", 0, 7, "Player", "retail")]
    [InlineData("report", 1, null, null, "retail")]
    [InlineData("report", 1, 0, " ", "retail")]
    [InlineData("report", 1, 7, "Player", "invalid")]
    public async Task GetRejectsInvalidRequestsWithoutCallingService(
        string report, int fight, int? actor, string? name, string edition)
    {
        var service = new TimelineService();
        var result = await Controller(service).Get(report, fight, actor, name, edition);
        Assert.Equal(400, Assert.IsType<ObjectResult>(result).StatusCode);
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
        var result = await Controller(service).Get("report", 3, useSourceId ? 7 : null,
            useSourceId ? null : "Player", edition, cancellation.Token, 264);
        Assert.Same(service.Player, Assert.IsType<OkObjectResult>(result).Value);
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
        var result = await Controller(service).GetBoss("report", 3, "retail", cancellation.Token);
        Assert.Same(service.Boss, Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(("report", 3, "retail", false), service.BossRequest);
        Assert.Equal(cancellation.Token, service.Cancellation);
    }

    [Fact]
    public async Task GetBossRejectsInvalidRequestsWithoutCallingService()
    {
        var service = new TimelineService();
        var result = await Controller(service).GetBoss("report", 0);
        Assert.Equal(400, Assert.IsType<ObjectResult>(result).StatusCode);
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
        var controller = Controller(new TimelineService { Failure = failure });
        var results = new[] { await controller.Get("report", 1, 7, null), await controller.GetBoss("report", 1) };
        foreach (var result in results)
        {
            var response = Assert.IsType<ObjectResult>(result);
            Assert.Equal(status, response.StatusCode);
            Assert.Equal(status, Assert.IsType<ProblemDetails>(response.Value).Status);
        }
    }

    [Fact]
    public async Task GetBossReturnsBadGatewayWhenReferenceCannotBeLoaded()
    {
        var result = await Controller(new TimelineService { Boss = null }).GetBoss("report", 1);
        Assert.Equal(502, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task CancellationIsNotConvertedIntoAnUpstreamError()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var controller = Controller(new TimelineService { Failure = new OperationCanceledException(cancellation.Token) });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => controller.Get("report", 1, 7, null,
            cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => controller.GetBoss("report", 1,
            cancellationToken: cancellation.Token));
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
        var controller = new ReportTimelineController(service, NullLogger<ReportTimelineController>.Instance);

        var result = await controller.Get("unavailable", 1, null, "Player", "retail");

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(expectedStatus, response.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(response.Value);
        Assert.Equal(expectedStatus, problem.Status);
        Assert.Contains(expectedDetail, problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    private static ReportTimelineController Controller(IReportTimelineService service) =>
        new(service, NullLogger<ReportTimelineController>.Instance);

    private sealed class TimelineService : IReportTimelineService
    {
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
