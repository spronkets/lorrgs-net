using Lorrgs.Tests.TestSupport;

namespace Lorrgs.WarcraftLogs.Tests;

public sealed class WarcraftLogsClientCacheTests : IDisposable
{
    private readonly TestClientFixture fixture = new();
    public void Dispose() => fixture.Dispose();

    [Fact]
    public async Task SuccessfulResponsesPersistAcrossClientsWithoutNetworkOrTokenRequests()
    {
        using var writerHandler = new QueueHandler("""{"data":{"value":42},"errors":[]}""");
        var writer = fixture.CreateClient(writerHandler);
        await writer.QueryGraphQLAsync("query Value", new { id = 1 });
        Assert.Single(Directory.GetFiles(fixture.CacheDirectory, "*.json"));
        using var readerHandler = new QueueHandler();
        var reader = fixture.CreateClient(readerHandler);

        var result = await reader.QueryGraphQLAsync("query Value", new { id = 1 });
        var cacheOnly = await reader.TryQueryGraphQLFromCacheAsync("query Value", new { id = 1 });

        Assert.Equal(42, result.GetProperty("value").GetInt32());
        Assert.Equal(result.GetRawText(), cacheOnly!.Value.GetRawText());
        Assert.Equal(0, readerHandler.GraphQlRequests);
        Assert.Equal(0, readerHandler.TokenRequests);
    }

    [Theory]
    [InlineData(23, true)]
    [InlineData(25, false)]
    public async Task FileCacheRetainsResponsesForTwentyFourHours(int ageHours, bool expectedHit)
    {
        using var handler = new QueueHandler("""{"data":{"value":1}}""", """{"data":{"value":2}}""");
        var client = fixture.CreateClient(handler);
        await client.QueryGraphQLAsync("query Age");
        var path = Assert.Single(Directory.GetFiles(fixture.CacheDirectory, "*.json"));
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-ageHours));

        var cached = await client.TryQueryGraphQLFromCacheAsync("query Age");
        Assert.Equal(expectedHit, cached.HasValue);
        Assert.Equal(expectedHit, File.Exists(path));
        var response = await client.QueryGraphQLAsync("query Age");

        Assert.Equal(expectedHit ? 1 : 2, response.GetProperty("value").GetInt32());
        Assert.Equal(expectedHit ? 1 : 2, handler.GraphQlRequests);
    }

    [Fact]
    public async Task CacheOnlyMissDoesNotRequestTokenOrGraphQl()
    {
        using var handler = new QueueHandler();
        Assert.Null(await fixture.CreateClient(handler).TryQueryGraphQLFromCacheAsync("missing"));
        Assert.Equal(0, handler.GraphQlRequests);
        Assert.Equal(0, handler.TokenRequests);
    }

    [Fact]
    public async Task BypassRefreshesExistingFileAndSubsequentRequestsUseNewValue()
    {
        using var handler = new QueueHandler("""{"data":{"value":1}}""", """{"data":{"value":2}}""");
        var client = fixture.CreateClient(handler);
        await client.QueryGraphQLAsync("query Refresh");
        var refreshed = await client.QueryGraphQLAsync("query Refresh", bypassCache: true);
        var cached = await client.QueryGraphQLAsync("query Refresh");
        Assert.Equal(2, refreshed.GetProperty("value").GetInt32());
        Assert.Equal(refreshed.GetRawText(), cached.GetRawText());
        Assert.Equal(2, handler.GraphQlRequests);
        Assert.Single(Directory.GetFiles(fixture.CacheDirectory, "*.json"));
    }

    [Fact]
    public async Task CacheKeysDistinguishQueriesVariablesAndEndpoints()
    {
        using var handler = new QueueHandler(
            """{"data":{"value":1}}""", """{"data":{"value":2}}""",
            """{"data":{"value":3}}""", """{"data":{"value":4}}""");
        var client = fixture.CreateClient(handler);
        await client.QueryGraphQLAsync("query A", new { id = 1 });
        await client.QueryGraphQLAsync("query A", new { id = 2 });
        await client.QueryGraphQLAsync("query B", new { id = 1 });
        var options = new Configuration.WarcraftLogsApiOptions
        {
            ClientId = "test", ClientSecret = "secret", BaseUrl = "https://other.test/api",
            AuthUrl = "https://wcl.test/oauth/token"
        };
        await fixture.CreateClient(handler, options).QueryGraphQLAsync("query A", new { id = 1 });
        Assert.Equal(4, handler.GraphQlRequests);
        Assert.Equal(4, Directory.GetFiles(fixture.CacheDirectory, "*.json").Length);
    }

    [Fact]
    public async Task SimultaneousIdenticalQueriesShareOneUpstreamResponse()
    {
        using var handler = new QueueHandler("""{"data":{"value":42}}""");
        var client = fixture.CreateClient(handler);
        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => client.QueryGraphQLAsync("query Concurrent")));
        Assert.All(responses, response => Assert.Equal(42, response.GetProperty("value").GetInt32()));
        Assert.Equal(1, handler.GraphQlRequests);
    }

    [Fact]
    public async Task ClearCacheRemovesFilesAndCachedToken()
    {
        using var handler = new QueueHandler("""{"data":{"value":1}}""", """{"data":{"value":2}}""");
        var client = fixture.CreateClient(handler);
        await client.QueryGraphQLAsync("query Clear");
        client.ClearCache();
        Assert.Empty(Directory.GetFiles(fixture.CacheDirectory, "*.json"));
        Assert.Null(await client.TryQueryGraphQLFromCacheAsync("query Clear"));
        await client.QueryGraphQLAsync("query Clear");
        Assert.Equal(2, handler.GraphQlRequests);
        Assert.Equal(2, handler.TokenRequests);
    }

    [Fact]
    public async Task CanceledRequestsDoNotReachUpstreamOrCreateCacheFiles()
    {
        using var handler = new QueueHandler();
        var client = fixture.CreateClient(handler);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.QueryGraphQLAsync("query Cancel", cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.QueryGraphQLAsync<System.Text.Json.JsonElement>("query Cancel", cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.TryQueryGraphQLFromCacheAsync("query Cancel", cancellationToken: cancellation.Token));
        Assert.Equal(0, handler.GraphQlRequests);
        Assert.Empty(Directory.GetFiles(fixture.CacheDirectory));
    }
}
