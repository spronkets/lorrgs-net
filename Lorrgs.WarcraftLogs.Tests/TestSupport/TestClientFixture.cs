using System.Net;
using System.Text;
using System.Text.Json;
using Lorrgs.WarcraftLogs;
using Lorrgs.WarcraftLogs.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Lorrgs.Tests.TestSupport;

internal sealed class TestClientFixture : IDisposable
{
    private readonly string root = Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N"));
    private readonly List<HttpClient> clients = [];
    private readonly List<MemoryCache> caches = [];
    public string CacheDirectory => Path.Combine(root, "Data", "cache", "wcl-graphql");

    public WarcraftLogsClient CreateClient(HttpMessageHandler handler, WarcraftLogsApiOptions? options = null)
    {
        options ??= new WarcraftLogsApiOptions
        {
            BaseUrl = "https://wcl.test/api",
            AuthUrl = "https://wcl.test/oauth/token",
            ClientId = "test-client",
            ClientSecret = "test-secret"
        };
        var httpClient = new HttpClient(handler, disposeHandler: false);
        var cache = new MemoryCache(new MemoryCacheOptions());
        clients.Add(httpClient);
        caches.Add(cache);
        return new WarcraftLogsClient(NullLogger<WarcraftLogsClient>.Instance,
            Options.Create(options), httpClient, cache, new TestHostEnvironment(root));
    }

    public void Dispose()
    {
        foreach (var client in clients) client.Dispose();
        foreach (var cache in caches) cache.Dispose();
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private sealed class TestHostEnvironment(string root) : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "LorrgsTests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = root;
        public string EnvironmentName { get; set; } = "Development";
    }
}

internal sealed class QueueHandler(params string[] graphQlResponses) : HttpMessageHandler
{
    private readonly Queue<string> responses = new(graphQlResponses);
    public int GraphQlRequests { get; private set; }
    public int TokenRequests { get; private set; }
    public List<JsonElement> Requests { get; } = [];
    public Exception? GraphQlFailure { get; init; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri?.AbsolutePath == "/oauth/token")
        {
            TokenRequests++;
            return JsonResponse("""{"access_token":"test-token","expires_in":3600}""");
        }
        GraphQlRequests++;
        using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
        Requests.Add(document.RootElement.Clone());
        if (GraphQlFailure is not null) throw GraphQlFailure;
        if (!responses.TryDequeue(out var payload))
            throw new InvalidOperationException("No queued GraphQL response remains.");
        return JsonResponse(payload);
    }

    private static HttpResponseMessage JsonResponse(string payload) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(payload, Encoding.UTF8, "application/json")
    };
}
