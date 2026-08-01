using System.Diagnostics;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

builder.Services.AddOptions<WarcraftLogsApiOptions>()
    .Bind(builder.Configuration.GetSection(WarcraftLogsApiOptions.SectionName))
    .ValidateOnStart();

builder.Services.AddOptions<RaidCatalogOptions>()
    .Bind(builder.Configuration)
    .ValidateOnStart();

builder.Services.AddMemoryCache();
builder.Services.AddScoped<Lorrgs.Api.Services.CacheService>();
builder.Services.AddScoped<Lorrgs.Api.Services.RotationAnalysisService>();
builder.Services.AddHostedService<Lorrgs.Api.Services.CacheCleanupBackgroundService>();

// HTTP client for WarcraftLogs API
builder.Services.AddHttpClient<Lorrgs.Api.Services.WarcraftLogsClient>();
builder.Services.AddScoped<Lorrgs.Api.Services.RaidCatalogClient>();
builder.Services.AddHealthChecks();

builder.Services.AddControllers();
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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "LorrgsNET API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseCors("lorrgs-web");
app.MapHealthChecks("/health");
app.MapControllers();

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
