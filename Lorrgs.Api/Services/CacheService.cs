using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

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
    private readonly TimeSpan _cacheExpiry;

    public CacheService(
        ILogger<CacheService> logger,
        IMemoryCache memoryCache,
        IWebHostEnvironment env,
        IOptions<CacheOptions> cacheOptions)
    {
        _logger = logger;
        _memoryCache = memoryCache;
        _cacheDirectory = Path.Combine(env.ContentRootPath, "Data", "cache");
        var ttlSeconds = Math.Max(60, cacheOptions.Value.DefaultTtlSeconds);
        _cacheExpiry = TimeSpan.FromSeconds(ttlSeconds);
        
        // Ensure cache directory exists
        if (!Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
            _logger.LogInformation("Created cache directory: {CacheDirectory}", _cacheDirectory);
        }

        _logger.LogInformation("Cache expiry configured to {CacheExpiryMinutes} minutes", _cacheExpiry.TotalMinutes);
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
                _memoryCache.Set(cacheKey, data, _cacheExpiry);
            }
            
            return data;
        }

        _logger.LogDebug("Cache MISS: {Category}/{Key}", category, key);
        return null;
    }

    /// <summary>
    /// Save data to cache (both file and memory).
    /// Uses a deterministic filename per key to avoid generating duplicate files.
    /// </summary>
    public async Task SetAsync<T>(string category, string key, T data) where T : class
    {
        var cacheKey = GetMemoryCacheKey(category, key);
        
        // Store in memory
        _memoryCache.Set(cacheKey, data, _cacheExpiry);
        
        // Store in file system
        var fileName = BuildCacheFileName(key);
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

        var deterministicPath = Path.Combine(directoryPath, BuildCacheFileName(key));
        if (File.Exists(deterministicPath))
        {
            try
            {
                File.Delete(deterministicPath);
                _logger.LogDebug("Deleted cache file: {FilePath}", deterministicPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete cache file: {FilePath}", deterministicPath);
            }
        }

        // Legacy cleanup for timestamp-based cache files.
        var legacyFiles = Directory.GetFiles(directoryPath, $"{key}_*.json");
        foreach (var file in legacyFiles)
        {
            try
            {
                File.Delete(file);
                _logger.LogDebug("Deleted legacy cache file: {FilePath}", file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete legacy cache file: {FilePath}", file);
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
            var age = DateTimeOffset.UtcNow - new DateTimeOffset(fileInfo.LastWriteTimeUtc);
            return age < _cacheExpiry;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Resolve cache file for a key.
    /// Prefers deterministic file name and falls back to legacy timestamp files.
    /// </summary>
    private string? GetLatestCacheFile(string category, string key)
    {
        var directoryPath = Path.Combine(_cacheDirectory, category);
        if (!Directory.Exists(directoryPath))
            return null;

        var deterministicPath = Path.Combine(directoryPath, BuildCacheFileName(key));
        if (File.Exists(deterministicPath))
        {
            return deterministicPath;
        }

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

    private static string BuildCacheFileName(string key)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant()[..12];

        var sanitizedChars = key
            .Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '-')
            .ToArray();
        var sanitized = new string(sanitizedChars).Trim('-');

        if (sanitized.Length > 60)
        {
            sanitized = sanitized[..60];
        }

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "cache";
        }

        return $"{sanitized}_{hash}.json";
    }

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
