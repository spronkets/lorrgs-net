using System.Diagnostics;
using Lorrgs.Api;
using Lorrgs.Api.Features.RaidCatalog;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using WclOptions = Lorrgs.WarcraftLogs.Configuration.WarcraftLogsApiOptions;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

builder.Services.AddOptions<WclOptions>()
    .Bind(builder.Configuration.GetSection(WclOptions.SectionName))
    .ValidateOnStart();

builder.Services.AddOptions<RaidCatalogOptions>()
    .Bind(builder.Configuration)
    .ValidateOnStart();

builder.Services.AddMemoryCache();
builder.Services.AddApiFeatures();
builder.Services.AddHealthChecks();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "LorrgsNET API",
        Version = "v1"
    });
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("lorrgs-web", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        if (context.Response.StatusCode >= StatusCodes.Status400BadRequest)
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.Remove("Expires");
        }

        return Task.CompletedTask;
    });

    await next(context);
});

app.UseCors("lorrgs-web");
app.MapHealthChecks("/health");
app.MapApi();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "LorrgsNET API v1");
        options.RoutePrefix = "swagger";
    });
}

app.Lifetime.ApplicationStarted.Register(() =>
{
    if (!app.Environment.IsDevelopment())
    {
        return;
    }

    var baseUrl = app.Urls.FirstOrDefault(url => url.StartsWith("http", StringComparison.OrdinalIgnoreCase));
    var swaggerUrl = baseUrl is null ? "http://localhost:5168/swagger" : $"{baseUrl.TrimEnd('/')}/swagger";

    try
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = swaggerUrl,
            UseShellExecute = true
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Unable to open Swagger UI automatically: {ex.Message}");
    }
});

app.Run();
