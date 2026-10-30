using Lorrgs.Tests.TestSupport;
using Lorrgs.Api.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace Lorrgs.Api.Tests;

public sealed class ApiEndpointFixture : IAsyncLifetime, IAsyncDisposable
{
    private readonly TestClientFixture upstreamFixture = new();
    internal QueueHandler Upstream { get; init; } = new();
    internal Action<IServiceCollection>? ConfigureServices { get; init; }
    public WebApplication App { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.AddApplicationLogging();
        builder.WebHost.UseTestServer();
        builder.Services.AddApiFeatures();
        builder.Services.AddSingleton(upstreamFixture.CreateClient(Upstream));
        ConfigureServices?.Invoke(builder.Services);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "Test API", Version = "v1" }));
        App = builder.Build();
        App.UseMiddleware<RequestLoggingMiddleware>();
        App.MapApi();
        App.UseSwagger();
        App.UseSwaggerUI();
        await App.StartAsync();
        Client = App.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await App.DisposeAsync();
        upstreamFixture.Dispose();
        Upstream.Dispose();
    }

    ValueTask IAsyncDisposable.DisposeAsync() => new(DisposeAsync());
}
