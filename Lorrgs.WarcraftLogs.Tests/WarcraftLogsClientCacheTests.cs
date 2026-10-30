using Lorrgs.Tests.TestSupport;

namespace Lorrgs.WarcraftLogs.Tests;

public sealed class WarcraftLogsClientCacheTests : IDisposable
{
    private readonly TestClientFixture fixture = new();
    public void Dispose() => fixture.Dispose();

    [Fact]
    public async Task IncompleteReportMetadataIsReturnedButNotCachedAndNextRequestRecovers()
    {
        using var handler = new QueueHandler(
            """{"data":{"reportData":{"report":{"fights":[],"masterData":{"actors":null,"abilities":null}}}}}""",
            """{"data":{"reportData":{"report":{"fights":[{"id":23}],"masterData":{"actors":[{"id":7}],"abilities":[]}}}}}""");
        var client = fixture.CreateClient(handler);

        var incomplete = await client.QueryGraphQLAsync("query Metadata");
        Assert.Empty(incomplete.GetProperty("reportData").GetProperty("report").GetProperty("fights").EnumerateArray());
        Assert.Empty(Directory.GetFiles(fixture.CacheDirectory, "*.json"));
        Assert.Null(await client.TryQueryGraphQLFromCacheAsync("query Metadata"));

        await client.QueryGraphQLAsync("query Metadata");
        await client.QueryGraphQLAsync("query Metadata");

        Assert.Equal(2, handler.GraphQlRequests);
        Assert.Single(Directory.GetFiles(fixture.CacheDirectory, "*.json"));
    }

    [Theory]
    [InlineData("""{"reportData":{"report":null}}""")]
    [InlineData("""{"reportData":{"report":{"fights":[]}}}""")]
    [InlineData("""{"reportData":{"r0":{"fights":[{"id":1}]},"r1":{"fights":[]}}}""")]
    [InlineData("""{"reportData":{"report":{"masterData":{"actors":null,"abilities":[]}}}}""")]
    [InlineData("""{"reportData":{"report":{"masterData":{"actors":[],"abilities":null}}}}""")]
    [InlineData("""{"reportData":{"report":{"fights":[{"id":1,"startTime":100,"endTime":100}]}}}""")]
    [InlineData("""{"reportData":{"report":{"events":{"data":null}}}}""")]
    [InlineData("""null""")]
    public async Task InvalidReportResponsesAreNotPersisted(string data)
    {
        using var handler = new QueueHandler($$"""{"data":{{data}}}""");
        await fixture.CreateClient(handler).QueryGraphQLAsync("query Invalid");

        Assert.Empty(Directory.GetFiles(fixture.CacheDirectory, "*.json"));
    }

    [Theory]
    [InlineData(true, """{"reportData":{"report":{"fights":[]}}}""")]
    [InlineData(false, """{"reportData":{"report":{"fights":[]}}}""")]
    [InlineData(true, """{"reportData":""")]
    [InlineData(false, """{"reportData":""")]
    public async Task PreviouslyCachedInvalidReportIsDiscarded(bool cacheOnlyFirst, string invalidData)
    {
        const string valid = """{"data":{"reportData":{"report":{"fights":[{"id":1}]}}}}""";
        using var handler = new QueueHandler(valid, valid);
        var client = fixture.CreateClient(handler);
        await client.QueryGraphQLAsync("query Existing");
        var path = Assert.Single(Directory.GetFiles(fixture.CacheDirectory, "*.json"));
        await File.WriteAllTextAsync(path, invalidData);

        if (cacheOnlyFirst)
        {
            Assert.Null(await client.TryQueryGraphQLFromCacheAsync("query Existing"));
            Assert.False(File.Exists(path));
            Assert.Equal(1, handler.GraphQlRequests);
        }
        await client.QueryGraphQLAsync("query Existing");

        Assert.Equal(2, handler.GraphQlRequests);
        Assert.NotNull(await client.TryQueryGraphQLFromCacheAsync("query Existing"));
    }

    [Theory]
    [InlineData("""{"reportData":{"report":{"events":{"data":[],"nextPageTimestamp":null}}}}""")]
    [InlineData("""{"worldData":{"encounter":{"characterRankings":{"rankings":[]}}}}""")]
    [InlineData("""{"reportData":{"report":{"rankings":{"data":[]}}}}""")]
    public async Task ValidEmptyEventPagesAndRankingsRemainCacheable(string data)
    {
        using var handler = new QueueHandler($$"""{"data":{{data}}}""");
        var client = fixture.CreateClient(handler);

        await client.QueryGraphQLAsync("query Empty");
        await client.QueryGraphQLAsync("query Empty");

        Assert.Equal(1, handler.GraphQlRequests);
        Assert.Single(Directory.GetFiles(fixture.CacheDirectory, "*.json"));
    }

    [Theory]
    [InlineData("""{"reportData":""")]
    [InlineData("""{"reportData":{"report":{"fights":[]}}}""")]
    public async Task UndeletableInvalidCacheEntryStillBehavesAsAMiss(string invalidData)
    {
        const string valid = """{"data":{"value":42}}""";
        using var handler = new QueueHandler(valid, valid);
        var client = fixture.CreateClient(handler);
        await client.QueryGraphQLAsync("query LockedInvalid");
        var path = Assert.Single(Directory.GetFiles(fixture.CacheDirectory, "*.json"));
        await File.WriteAllTextAsync(path, invalidData);
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.Null(await client.TryQueryGraphQLFromCacheAsync("query LockedInvalid"));
            var fresh = await client.QueryGraphQLAsync("query LockedInvalid");
            Assert.Equal(42, fresh.GetProperty("value").GetInt32());
        }
        Assert.Equal(2, handler.GraphQlRequests);
    }

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
    [InlineData(239, true)]
    [InlineData(241, false)]
    public async Task FileCacheRetainsResponsesForTenDays(int ageHours, bool expectedHit)
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
    public async Task ConfiguredTimeSpanTtlIsUsedWithOneMinuteMinimum()
    {
        using var handler = new QueueHandler("""{"data":{"value":1}}""");
        var options = new Configuration.WarcraftLogsApiOptions
        {
            ClientId = "test", ClientSecret = "secret", BaseUrl = "https://wcl.test/api",
            AuthUrl = "https://wcl.test/oauth/token", GraphQlCacheTtl = TimeSpan.FromSeconds(1)
        };
        var client = fixture.CreateClient(handler, options);
        await client.QueryGraphQLAsync("query Age");
        var path = Assert.Single(Directory.GetFiles(fixture.CacheDirectory, "*.json"));

        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(-30));
        Assert.NotNull(await client.TryQueryGraphQLFromCacheAsync("query Age"));
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(-90));
        Assert.Null(await client.TryQueryGraphQLFromCacheAsync("query Age"));
        Assert.Equal(1, handler.GraphQlRequests);
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
    public async Task DistinctQueriesRunConcurrentlyWithOneSharedTokenRequest()
    {
        var tokenStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tokenReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queriesStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queriesReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tokenCount = 0;
        var queryCount = 0;
        using var handler = new CallbackHandler(async (request, cancellationToken) =>
        {
            if (request.RequestUri!.AbsolutePath == "/oauth/token")
            {
                Interlocked.Increment(ref tokenCount);
                tokenStarted.TrySetResult();
                await tokenReady.Task.WaitAsync(cancellationToken);
                return """{"access_token":"test-token","expires_in":3600}""";
            }
            if (Interlocked.Increment(ref queryCount) == 3) queriesStarted.TrySetResult();
            await queriesReady.Task.WaitAsync(cancellationToken);
            return """{"data":{"value":42}}""";
        });
        var client = fixture.CreateClient(handler);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var tasks = Enumerable.Range(0, 3).Select(index =>
            client.QueryGraphQLAsync($"query Concurrent{index}", cancellationToken: cancellation.Token)).ToArray();
        try
        {
            await tokenStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, Volatile.Read(ref tokenCount));
            Assert.Equal(0, Volatile.Read(ref queryCount));
            tokenReady.SetResult();
            await queriesStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.All(tasks, task => Assert.False(task.IsCompleted));
        }
        finally
        {
            tokenReady.TrySetResult();
            queriesReady.TrySetResult();
            await Task.WhenAll(tasks);
        }
        Assert.Equal(1, tokenCount);
        Assert.Equal(3, queryCount);
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
    public async Task ParentCancellationCancelsAllConcurrentRequestsWithoutCachingResponses()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        var canceled = 0;
        using var handler = new CallbackHandler(async (request, cancellationToken) =>
        {
            if (request.RequestUri!.AbsolutePath == "/oauth/token")
                return """{"access_token":"token","expires_in":3600}""";
            if (Interlocked.Increment(ref count) == 3) started.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Interlocked.Increment(ref canceled);
                throw;
            }
            throw new InvalidOperationException("Request should have been canceled.");
        });
        var client = fixture.CreateClient(handler);
        using var cancellation = new CancellationTokenSource();
        var tasks = Enumerable.Range(0, 3).Select(index =>
            client.QueryGraphQLAsync($"query Cascade{index}", cancellationToken: cancellation.Token)).ToArray();
        try { await started.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { cancellation.Cancel(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.All(tasks, task => Assert.True(task.IsCanceled));
        Assert.Equal(3, canceled);
        Assert.Empty(Directory.GetFiles(fixture.CacheDirectory));
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
