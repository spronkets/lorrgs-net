using Lorrgs.Api.Features.RaidCatalog;
using Lorrgs.Api.Features.RaidCatalog.GetCatalog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Lorrgs.Api.Tests;

public sealed class RaidCatalogHandlerTests
{
    [Fact]
    public async Task CanceledCatalogRequestsPropagateCancellation()
    {
        var client = new RaidCatalogClient(NullLogger<RaidCatalogClient>.Instance,
            Options.Create(new RaidCatalogOptions()));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var handler = new GetCatalogHandler(NullLogger<GetCatalogHandler>.Instance, client);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.Handle(cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetCatalogAsync(cancellation.Token));
    }

    [Fact]
    public async Task GetCatalogReturnsTheConfiguredCatalogFromTheService()
    {
        var client = new RaidCatalogClient(NullLogger<RaidCatalogClient>.Instance,
            Options.Create(new RaidCatalogOptions
            {
                Editions = [new() { Slug = "retail", Name = "Retail" }]
            }));
        var logger = new RecordingLogger();
        var handlers = new GetCatalogHandler(logger, client);

        var response = HttpResultAssertions.Status(200, await handlers.Handle());

        var catalog = Assert.IsType<RaidCatalog>(response.Value);
        Assert.Equal("retail", Assert.Single(catalog.Editions).Slug);
        Assert.Empty(catalog.Instances["retail"]);
        Assert.NotSame(await client.GetCatalogAsync(), catalog);
        var properties = Assert.Single(logger.Entries);
        Assert.Equal(catalog.Editions.Count, properties.Single(property => property.Key == "EditionCount").Value);
        Assert.Equal(catalog.Instances.Values.Sum(instances => instances.Count),
            properties.Single(property => property.Key == "InstanceCount").Value);
    }

    private sealed class RecordingLogger : ILogger<GetCatalogHandler>
    {
        public List<List<KeyValuePair<string, object?>>> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Assert.Equal(LogLevel.Information, logLevel);
            Assert.Null(exception);
            Entries.Add(((IEnumerable<KeyValuePair<string, object?>>)state!).ToList());
        }
    }
}
