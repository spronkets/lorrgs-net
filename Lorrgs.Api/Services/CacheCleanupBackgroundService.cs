namespace Lorrgs.Api.Services;

using Microsoft.Extensions.Options;

/// <summary>
/// Background service that periodically cleans up expired cache files.
/// Runs every hour automatically after the API starts.
/// </summary>
public class CacheCleanupBackgroundService(
    ILogger<CacheCleanupBackgroundService> logger,
    IServiceProvider serviceProvider,
    IOptions<CacheOptions> cacheOptions) : BackgroundService
{
    private readonly ILogger<CacheCleanupBackgroundService> _logger = logger;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(Math.Max(1, cacheOptions.Value.CleanupIntervalMinutes));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Allow the application to start before beginning cleanup
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting cache cleanup task");
                
                // Create a scope to resolve the scoped CacheService
                using (var scope = _serviceProvider.CreateScope())
                {
                    var cacheService = scope.ServiceProvider.GetRequiredService<CacheService>();
                    await cacheService.CleanupExpiredAsync();
                }
                
                _logger.LogInformation("Cache cleanup completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache cleanup");
            }

            // Wait for the next cleanup interval
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Expected when service is stopping
                break;
            }
        }

        _logger.LogInformation("Cache cleanup background service stopped");
    }
}
