using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Lorrgs.Tests.TestSupport;
using Lorrgs.WarcraftLogs.Configuration;

namespace Lorrgs.WarcraftLogs.Tests;

public sealed class WarcraftLogsClientHttpTests : IDisposable
{
    private readonly TestClientFixture fixture = new();
    public void Dispose() => fixture.Dispose();

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task TransientFailuresRetryTheSameAuthorizedPayload(HttpStatusCode status)
    {
        using var failure = JsonResponse("{}", status);
        failure.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(1));
        using var handler = new ResponseHandler(failure, JsonResponse("""{"data":{"ok":true}}"""));
        var client = fixture.CreateClient(handler);

        var result = await client.QueryGraphQLAsync("query Retry", new { id = 7 });

        Assert.True(result.GetProperty("ok").GetBoolean());
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(handler.Requests[0], handler.Requests[1]);
        Assert.All(handler.Authorizations, value => Assert.Equal("Bearer test-token", value));
        Assert.Single(Directory.GetFiles(fixture.CacheDirectory, "*.json"));
    }

    [Fact]
    public async Task TransientFailuresStopAfterThreeAttemptsAndNeverCacheFailure()
    {
        using var handler = new ResponseHandler(
            JsonResponse("{}", HttpStatusCode.BadGateway),
            JsonResponse("{}", HttpStatusCode.BadGateway),
            JsonResponse("{}", HttpStatusCode.BadGateway));
        var client = fixture.CreateClient(handler);
        var failure = await Assert.ThrowsAsync<HttpRequestException>(() => client.QueryGraphQLAsync("query Exhaust"));
        Assert.Equal(HttpStatusCode.BadGateway, failure.StatusCode);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Empty(Directory.GetFiles(fixture.CacheDirectory));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task NonRetryableHttpFailuresAreSurfacedImmediatelyAndNotCached(HttpStatusCode status)
    {
        using var handler = new ResponseHandler(JsonResponse("{}", status));
        var client = fixture.CreateClient(handler);
        var failure = await Assert.ThrowsAsync<HttpRequestException>(() => client.QueryGraphQLAsync("query Failure"));
        Assert.Equal(status, failure.StatusCode);
        Assert.Single(handler.Requests);
        Assert.Empty(Directory.GetFiles(fixture.CacheDirectory));
    }

    [Theory]
    [InlineData("delta")]
    [InlineData("date")]
    [InlineData("missing")]
    public async Task RateLimitCooldownPreventsAdditionalUpstreamRequests(string retryAfter)
    {
        using var response = JsonResponse("{}", HttpStatusCode.TooManyRequests);
        if (retryAfter == "delta") response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(2));
        if (retryAfter == "date") response.Headers.RetryAfter = new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddMinutes(2));
        using var handler = new ResponseHandler(response);
        var client = fixture.CreateClient(handler);

        var initial = await Assert.ThrowsAsync<HttpRequestException>(() => client.QueryGraphQLAsync("query Limited"));
        var subsequent = await Assert.ThrowsAsync<HttpRequestException>(() => client.QueryGraphQLAsync("query Different"));

        Assert.Equal(HttpStatusCode.TooManyRequests, initial.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, subsequent.StatusCode);
        Assert.Single(handler.Requests);
        Assert.Empty(Directory.GetFiles(fixture.CacheDirectory));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"access_token\":\" \"}")]
    public async Task InvalidOAuthResponsesAreExplicitFailures(string tokenJson)
    {
        using var handler = new ResponseHandler { TokenJson = tokenJson };
        var client = fixture.CreateClient(handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.QueryGraphQLAsync("query Token"));
        Assert.Empty(handler.Requests);
        Assert.Empty(Directory.GetFiles(fixture.CacheDirectory));
    }

    [Fact]
    public async Task MissingCredentialsFailBeforeMakingAnyHttpRequests()
    {
        using var handler = new ResponseHandler();
        var client = fixture.CreateClient(handler, new WarcraftLogsApiOptions());
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetClientTokenAsync("https://wcl.test/oauth/token"));
        Assert.Contains("credentials not configured", failure.Message);
        Assert.Equal(0, handler.TokenRequests);
    }

    [Fact]
    public async Task OAuthTokenIsReusedEvenWhenExpiryIsOmitted()
    {
        using var handler = new ResponseHandler { TokenJson = """{"access_token":"test-token"}""" };
        var client = fixture.CreateClient(handler);
        Assert.Equal("test-token", await client.GetClientTokenAsync("https://wcl.test/oauth/token"));
        Assert.Equal("test-token", await client.GetClientTokenAsync("https://wcl.test/oauth/token"));
        Assert.Equal(1, handler.TokenRequests);
    }

    [Fact]
    public async Task TypedQueriesDeserializeCaseInsensitivePropertiesAndRejectNullResults()
    {
        using var handler = new ResponseHandler(JsonResponse("""{"data":{"VALUE":42}}"""), JsonResponse("""{"data":null}"""));
        var client = fixture.CreateClient(handler);
        Assert.Equal(42, (await client.QueryGraphQLAsync<ValueResponse>("query Typed")).Value);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.QueryGraphQLAsync<ValueResponse>("query Null"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("not json")]
    public async Task MissingDataAndMalformedResponsesAreNeverCached(string json)
    {
        using var handler = new ResponseHandler(JsonResponse(json));
        var client = fixture.CreateClient(handler);
        if (json == "{}")
            await Assert.ThrowsAsync<InvalidOperationException>(() => client.QueryGraphQLAsync("query Invalid"));
        else
            await Assert.ThrowsAnyAsync<JsonException>(() => client.QueryGraphQLAsync("query Invalid"));
        Assert.Empty(Directory.GetFiles(fixture.CacheDirectory));
    }

    private sealed record ValueResponse(int Value);

    private static HttpResponseMessage JsonResponse(string json, HttpStatusCode status = HttpStatusCode.OK) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class ResponseHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses = new(responses);
        public string TokenJson { get; init; } = """{"access_token":"test-token","expires_in":3600}""";
        public int TokenRequests { get; private set; }
        public List<string> Requests { get; } = [];
        public List<string?> Authorizations { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath == "/oauth/token")
            {
                TokenRequests++;
                return JsonResponse(TokenJson);
            }
            Requests.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            Authorizations.Add(request.Headers.Authorization?.ToString());
            if (!responses.TryDequeue(out var response))
                throw new InvalidOperationException("No queued response remains.");
            return response;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                foreach (var response in responses) response.Dispose();
            base.Dispose(disposing);
        }
    }
}
