using System.Net;
using System.Text.Json;
using Lorrgs.Api.Controllers;
using Lorrgs.Api.Models;
using Lorrgs.Api.Services;
using Lorrgs.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lorrgs.Api.Tests;

public sealed class WarcraftLogsControllerTests : IDisposable
{
    private readonly TestClientFixture fixture = new();
    public void Dispose() => fixture.Dispose();

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task LookupRejectsBlankCodeBeforeSendingRequests(string code)
    {
        using var handler = new QueueHandler();
        Assert.IsType<BadRequestObjectResult>(await Controller(handler).LookupReport(code));
        Assert.Equal(0, handler.GraphQlRequests);
    }

    [Fact]
    public async Task LookupForwardsTrimmedCodeAndReturnsUpstreamMetadata()
    {
        using var handler = new QueueHandler("""{"data":{"reportData":{"report":{"code":"abc","title":"Raid"}}}}""");
        var result = Assert.IsType<OkObjectResult>(await Controller(handler).LookupReport(" abc "));
        var data = Assert.IsType<JsonElement>(result.Value);
        Assert.Equal("Raid", data.GetProperty("reportData").GetProperty("report").GetProperty("title").GetString());
        Assert.Equal("abc", Assert.Single(handler.Requests).GetProperty("variables").GetProperty("code").GetString());
    }

    [Theory]
    [InlineData(true, 429)]
    [InlineData(false, 500)]
    public async Task LookupAndAnalysisPreserveRateLimitAndUnexpectedFailureStatus(bool rateLimited, int expected)
    {
        Exception failure = rateLimited
            ? new HttpRequestException("Rate limited", null, HttpStatusCode.TooManyRequests)
            : new JsonException("Invalid response");
        using var handler = new QueueHandler { GraphQlFailure = failure };
        var controller = Controller(handler);

        var lookup = Assert.IsType<ObjectResult>(await controller.LookupReport("abc"));
        var analysis = Assert.IsType<ObjectResult>(await controller.AnalyzeRotation(new() { ReportCode = "abc" }));

        Assert.Equal(expected, lookup.StatusCode);
        Assert.Equal(expected, analysis.StatusCode);
        Assert.Equal(2, handler.GraphQlRequests);
    }

    [Fact]
    public async Task AnalysisReturnsBadRequestForInvalidInputAndNotFoundForMissingReport()
    {
        using var handler = new QueueHandler("""{"data":{}}""");
        var controller = Controller(handler);
        Assert.IsType<BadRequestObjectResult>(await controller.AnalyzeRotation(new() { ReportCode = " " }));
        var missing = Assert.IsType<NotFoundObjectResult>(await controller.AnalyzeRotation(new() { ReportCode = "abc" }));
        Assert.Contains("Report not found", JsonSerializer.Serialize(missing.Value));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task AnalysisReturnsSuccessfulServiceResult()
    {
        using var handler = new QueueHandler("""
            {"data":{"reportData":{"report":{"fights":[{"id":1,"name":"Boss","startTime":0,"endTime":60000}],
            "masterData":{"actors":[{"id":7,"name":"Player","subType":"Holy"}]}}}}}
            """, """{"data":{"reportData":{"report":{"events":{"data":[]}}}}}""");

        var response = Assert.IsType<OkObjectResult>(await Controller(handler).AnalyzeRotation(
            new() { ReportCode = "abc", FightId = 1, SourceId = 7 }));

        var analysis = Assert.IsType<RotationAnalysisResponse>(response.Value);
        Assert.Equal("abc", analysis.ReportCode);
        Assert.Equal("Player", analysis.PlayerName);
        Assert.Equal("healer", analysis.RoleHint);
        Assert.Empty(analysis.RotationTimeline);
    }

    private WarcraftLogsController Controller(QueueHandler handler)
    {
        var client = fixture.CreateClient(handler);
        return new(NullLogger<WarcraftLogsController>.Instance,
            new RotationAnalysisService(NullLogger<RotationAnalysisService>.Instance, client), client);
    }
}
