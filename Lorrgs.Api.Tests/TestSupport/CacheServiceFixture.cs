using Lorrgs.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Lorrgs.Api.Tests.TestSupport;

internal sealed class CacheServiceFixture : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "LorrgsApiTests", Guid.NewGuid().ToString("N"));
    private readonly List<MemoryCache> caches = [];

    public CacheService CreateService()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        caches.Add(cache);
        return new CacheService(NullLogger<CacheService>.Instance, cache,
            new TestEnvironment(root), Options.Create(new CacheOptions()));
    }

    public void Dispose()
    {
        foreach (var cache in caches) cache.Dispose();
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private sealed class TestEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "LorrgsApiTests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
