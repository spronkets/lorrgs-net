using System.Diagnostics;
using Lorrgs.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Lorrgs.Api.Tests;

public sealed class RequestLoggingMiddlewareTests
{
    [Theory]
    [InlineData(200)]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(502)]
    public async Task CorrelatesDownstreamLogsAndResponseWithoutRecordingRequestValues(int status)
    {
        using var activity = new Activity("request").SetIdFormat(ActivityIdFormat.W3C).Start();
        var logger = new RecordingLogger();
        var context = new DefaultHttpContext { TraceIdentifier = "server-request-id" };
        context.Request.Path = "/reports/unverified";
        context.Request.QueryString = new QueryString("?playerName=unverified-secret");
        context.Request.Headers["X-Correlation-ID"] = "unverified-client-value";
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "GetPlayerTimeline"));
        var middleware = new RequestLoggingMiddleware(async current =>
        {
            await Task.Yield();
            Assert.NotNull(logger.Scope);
            Assert.Equal(activity.TraceId.ToString(), logger.Scope["CorrelationId"]);
            current.Response.StatusCode = status;
        }, logger);

        await middleware.InvokeAsync(context);

        Assert.Equal(activity.TraceId.ToString(), context.Response.Headers["X-Correlation-ID"]);
        Assert.Equal("server-request-id", logger.Scope!["RequestId"]);
        Assert.All(logger.Messages, message => Assert.DoesNotContain("unverified", message));
        if (status >= 400)
            Assert.Contains($"endpoint=GetPlayerTimeline status={status}", Assert.Single(logger.Messages));
        else
            Assert.Empty(logger.Messages);
    }

    private sealed class RecordingLogger : ILogger<RequestLoggingMiddleware>
    {
        public Dictionary<string, object?>? Scope { get; private set; }
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            Scope = Assert.IsAssignableFrom<IEnumerable<KeyValuePair<string, object?>>>(state).ToDictionary();
            return null;
        }
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
