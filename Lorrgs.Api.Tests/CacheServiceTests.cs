using Lorrgs.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Lorrgs.Api.Tests;

public sealed class CacheServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "LorrgsCacheTests", Guid.NewGuid().ToString("N"));
    private readonly List<MemoryCache> caches = [];
    private string CategoryDirectory => Path.Combine(root, "Data", "cache", "reports");

    [Fact]
    public async Task SetReplacesDeterministicFileAndReadsPersistAcrossServiceInstances()
    {
        var service = Service();
        await service.SetAsync("reports", "report", new Entry("first"));
        Assert.Equal(new Entry("first"), await service.GetAsync<Entry>("reports", "report"));
        await service.SetAsync("reports", "report", new Entry("updated"));
        Assert.Single(Directory.GetFiles(CategoryDirectory));
        Assert.Equal(new Entry("updated"), await Service().GetAsync<Entry>("reports", "report"));
    }

    [Theory]
    [InlineData(23, true)]
    [InlineData(25, false)]
    public async Task FilesFollowTwentyFourHourDefaultExpiry(int ageHours, bool expectedHit)
    {
        await Service().SetAsync("reports", "report", new Entry("stored"));
        File.SetLastWriteTimeUtc(Assert.Single(Directory.GetFiles(CategoryDirectory)), DateTime.UtcNow.AddHours(-ageHours));
        var result = await Service().GetAsync<Entry>("reports", "report");
        Assert.Equal(expectedHit ? new Entry("stored") : null, result);
    }

    [Fact]
    public async Task TtlIsClampedToAtLeastSixtySeconds()
    {
        await Service(ttl: 1).SetAsync("reports", "report", new Entry("stored"));
        File.SetLastWriteTimeUtc(Assert.Single(Directory.GetFiles(CategoryDirectory)), DateTime.UtcNow.AddSeconds(-30));
        Assert.NotNull(await Service(ttl: 1).GetAsync<Entry>("reports", "report"));
    }

    [Fact]
    public async Task MissesAndCorruptOrNullFilesReturnNoData()
    {
        var service = Service();
        Assert.Null(await service.GetAsync<Entry>("missing", "key"));
        Directory.CreateDirectory(CategoryDirectory);
        Assert.Null(await service.GetAsync<Entry>("reports", "key"));
        await File.WriteAllTextAsync(Path.Combine(CategoryDirectory, "key_old.json"), "not json");
        Assert.Null(await service.GetAsync<Entry>("reports", "key"));
        await File.WriteAllTextAsync(Path.Combine(CategoryDirectory, "key_old.json"), "null");
        Assert.Null(await service.GetAsync<Entry>("reports", "key"));
    }

    [Fact]
    public async Task ReadsNewestLegacyFileAndPromotesItToMemory()
    {
        var service = Service();
        Directory.CreateDirectory(CategoryDirectory);
        var older = Path.Combine(CategoryDirectory, "report_old.json");
        var newer = Path.Combine(CategoryDirectory, "report_new.json");
        await File.WriteAllTextAsync(older, """{"Value":"old"}""");
        await File.WriteAllTextAsync(newer, """{"Value":"new"}""");
        File.SetLastWriteTimeUtc(older, DateTime.UtcNow.AddHours(-2));
        File.SetLastWriteTimeUtc(newer, DateTime.UtcNow.AddHours(-1));
        Assert.Equal(new Entry("new"), await service.GetAsync<Entry>("reports", "report"));
        File.Delete(newer);
        Assert.Equal(new Entry("new"), await service.GetAsync<Entry>("reports", "report"));
    }

    [Fact]
    public async Task InvalidateRemovesMemoryDeterministicAndLegacyFilesWithoutAffectingOtherKeys()
    {
        var service = Service();
        await service.SetAsync("reports", "report", new Entry("value"));
        await service.SetAsync("reports", "other", new Entry("retained"));
        var legacy = Path.Combine(CategoryDirectory, "report_old.json");
        await File.WriteAllTextAsync(legacy, "{}");
        await service.InvalidateAsync("reports", "report");
        await service.InvalidateAsync("absent", "report");
        Assert.False(File.Exists(legacy));
        Assert.Single(Directory.GetFiles(CategoryDirectory));
        Assert.Null(await service.GetAsync<Entry>("reports", "report"));
        Assert.Null(await Service().GetAsync<Entry>("reports", "report"));
        Assert.Equal(new Entry("retained"), await service.GetAsync<Entry>("reports", "other"));
    }

    [Fact]
    public async Task CleanupRemovesOnlyExpiredJsonFilesAcrossCategories()
    {
        var service = Service();
        await service.SetAsync("reports", "fresh", new Entry("fresh"));
        await service.SetAsync("reports", "stale", new Entry("stale"));
        await service.SetAsync("other", "stale", new Entry("other stale"));
        var staleFiles = Directory.GetFiles(Path.Combine(root, "Data", "cache"), "stale_*.json", SearchOption.AllDirectories);
        foreach (var file in staleFiles) File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddHours(-25));
        var nonJson = Path.Combine(CategoryDirectory, "keep.txt");
        await File.WriteAllTextAsync(nonJson, "keep");

        await service.CleanupExpiredAsync();
        await service.CleanupExpiredAsync();

        Assert.All(staleFiles, file => Assert.False(File.Exists(file)));
        Assert.True(File.Exists(nonJson));
        Assert.Equal(new Entry("fresh"), await Service().GetAsync<Entry>("reports", "fresh"));
        Assert.Null(await Service().GetAsync<Entry>("other", "stale"));
    }

    [Theory]
    [InlineData("https://example.test/a?x=1")]
    [InlineData("!!!")]
    [InlineData("a-very-long-key-that-exceeds-sixty-characters-in-order-to-test-the-filename-length-limit")]
    public async Task FileNamesAreBoundedAndSanitizedWithoutKeyCollisions(string key)
    {
        var service = Service();
        await service.SetAsync("reports", key, new Entry("first"));
        await service.SetAsync("reports", key + "!", new Entry("second"));
        var files = Directory.GetFiles(CategoryDirectory);
        Assert.Equal(2, files.Length);
        Assert.All(files, file => Assert.InRange(Path.GetFileName(file).Length, 1, 78));
        Assert.Equal(new Entry("first"), await Service().GetAsync<Entry>("reports", key));
        Assert.Equal(new Entry("second"), await Service().GetAsync<Entry>("reports", key + "!"));
    }

    private CacheService Service(int ttl = 86400)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        caches.Add(cache);
        return new CacheService(NullLogger<CacheService>.Instance, cache,
            new TestEnvironment { ContentRootPath = root }, Options.Create(new CacheOptions { DefaultTtlSeconds = ttl }));
    }

    public void Dispose()
    {
        foreach (var cache in caches) cache.Dispose();
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    public sealed record Entry(string Value);

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "LorrgsCacheTests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
