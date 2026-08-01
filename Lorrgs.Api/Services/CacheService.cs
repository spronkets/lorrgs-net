using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace Lorrgs.Api.Services;

/// <summary>
/// Manages file-based caching of WarcraftLogs API responses.
/// Uses JSON files with automatic expiry (1 day) and in-memory cache for performance.
/// </summary>
public class CacheService
{
    private readonly ILogger<CacheService> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly string _cacheDirectory;
    private const int CacheExpirySeconds = 86400; // 1 day

    public CacheService(ILogger<CacheService> logger, IMemoryCache memoryCache, IWebHostEnvironment env)
    {
        _logger = logger;
        _memoryCache = memoryCache;
        _cacheDirectory = Path.Combine(env.ContentRootPath, "Data", "cache");
        
        // Ensure cache directory exists
        if (!Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
            _logger.LogInformation("Created cache directory: {CacheDirectory}", _cacheDirectory);
        }
    }

    /// <summary>
    /// Get cached data by key. Tries in-memory cache first, then file system.
    /// </summary>
    public async Task<T?> GetAsync<T>(string category, string key) where T : class
    {
        var cacheKey = GetMemoryCacheKey(category, key);

        // Try in-memory cache first
        if (_memoryCache.TryGetValue(cacheKey, out T? cached))
        {
            _logger.LogDebug("Cache HIT (memory): {Category}/{Key}", category, key);
            return cached;
        }

        // Try file system cache
        var filePath = GetLatestCacheFile(category, key);
        if (filePath != null && IsValidCacheFile(filePath))
        {
            _logger.LogDebug("Cache HIT (file): {FilePath}", filePath);
            var data = await ReadCacheFileAsync<T>(filePath);
            
            if (data != null)
            {
                // Store in memory cache for next request
                _memoryCache.Set(cacheKey, data, TimeSpan.FromSeconds(CacheExpirySeconds));
            }
            
            return data;
        }

        _logger.LogDebug("Cache MISS: {Category}/{Key}", category, key);
        return null;
    }

    /// <summary>
    /// Save data to cache (both file and memory).
    /// Automatically adds timestamp for expiry tracking.
    /// </summary>
    public async Task SetAsync<T>(string category, string key, T data) where T : class
    {
        var cacheKey = GetMemoryCacheKey(category, key);
        
        // Store in memory
        _memoryCache.Set(cacheKey, data, TimeSpan.FromSeconds(CacheExpirySeconds));
        
        // Store in file system
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var fileName = $"{key}_{timestamp}.json";
        var directoryPath = Path.Combine(_cacheDirectory, category);
        var filePath = Path.Combine(directoryPath, fileName);

        try
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = false });
            await File.WriteAllTextAsync(filePath, json);
            
            _logger.LogDebug("Cache saved: {FilePath}", filePath);

            // Clean up old files for this key
            await CleanupOldFilesAsync(category, key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save cache: {FilePath}", filePath);
        }
    }

    /// <summary>
    /// Clear a specific cache entry from both memory and file system.
    /// </summary>
    public async Task InvalidateAsync(string category, string key)
    {
        var cacheKey = GetMemoryCacheKey(category, key);
        _memoryCache.Remove(cacheKey);

        var directoryPath = Path.Combine(_cacheDirectory, category);
        if (!Directory.Exists(directoryPath))
            return;

        var files = Directory.GetFiles(directoryPath, $"{key}_*.json");
        foreach (var file in files)
        {
            try
            {
                File.Delete(file);
                _logger.LogDebug("Deleted cache file: {FilePath}", file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete cache file: {FilePath}", file);
            }
        }
    }

    /// <summary>
    /// Check if cache is expired based on file modification time.
    /// </summary>
    private bool IsValidCacheFile(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            var ageSeconds = (DateTimeOffset.UtcNow - new DateTimeOffset(fileInfo.LastWriteTimeUtc)).TotalSeconds;
            return ageSeconds < CacheExpirySeconds;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Find the most recent cache file for a key.
    /// Format: {key}_{timestamp}.json
    /// </summary>
    private string? GetLatestCacheFile(string category, string key)
    {
        var directoryPath = Path.Combine(_cacheDirectory, category);
        if (!Directory.Exists(directoryPath))
            return null;

        var files = Directory.GetFiles(directoryPath, $"{key}_*.json");
        if (files.Length == 0)
            return null;

        // Return the most recent file (highest timestamp)
        var latestFile = files[0];
        foreach (var file in files)
        {
            if (File.GetLastWriteTimeUtc(file) > File.GetLastWriteTimeUtc(latestFile))
                latestFile = file;
        }

        return latestFile;
    }

    /// <summary>
    /// Delete old cache files for a given key.
    /// Keeps only the most recent file.
    /// </summary>
    private async Task CleanupOldFilesAsync(string category, string key)
    {
        var directoryPath = Path.Combine(_cacheDirectory, category);
        if (!Directory.Exists(directoryPath))
            return;

        var files = Directory.GetFiles(directoryPath, $"{key}_*.json");
        if (files.Length <= 1)
            return;

        // Sort by modification time, keep only the latest
        Array.Sort(files, (a, b) => 
            File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a))
        );

        for (int i = 1; i < files.Length; i++)
        {
            try
            {
                File.Delete(files[i]);
                _logger.LogDebug("Cleaned up old cache: {FilePath}", files[i]);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clean old cache: {FilePath}", files[i]);
            }
        }
    }

    /// <summary>
    /// Background cleanup task - deletes all expired cache files.
    /// Should be called hourly.
    /// </summary>
    public async Task CleanupExpiredAsync()
    {
        if (!Directory.Exists(_cacheDirectory))
            return;

        var categories = Directory.GetDirectories(_cacheDirectory);
        var deletedCount = 0;

        foreach (var categoryPath in categories)
        {
            var files = Directory.GetFiles(categoryPath, "*.json");
            foreach (var file in files)
            {
                if (!IsValidCacheFile(file))
                {
                    try
                    {
                        File.Delete(file);
                        deletedCount++;
                        _logger.LogDebug("Cleaned expired cache: {FilePath}", file);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to delete expired cache: {FilePath}", file);
                    }
                }
            }
        }

        if (deletedCount > 0)
        {
            _logger.LogInformation("Cleaned {DeletedCount} expired cache files", deletedCount);
        }
    }

    private string GetMemoryCacheKey(string category, string key) => $"cache:{category}:{key}";

    private async Task<T?> ReadCacheFileAsync<T>(string filePath) where T : class
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read cache file: {FilePath}", filePath);
            return null;
        }
    }
}
