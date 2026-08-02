using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Lorrgs.WarcraftLogs.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lorrgs.WarcraftLogs;

public class WarcraftLogsClient(
    ILogger<WarcraftLogsClient> logger,
    IOptions<WarcraftLogsApiOptions> options,
    HttpClient httpClient,
    IMemoryCache cache,
    IHostEnvironment hostEnvironment)
{
    private readonly ILogger<WarcraftLogsClient> _logger = logger;
    private readonly WarcraftLogsApiOptions _options = options.Value;
    private readonly HttpClient _httpClient = httpClient;
    private readonly IMemoryCache _cache = cache;
    private readonly string _graphQlCacheDirectory = InitializeGraphQlCacheDirectory(hostEnvironment.ContentRootPath);

    private const string TokenCacheKeyPrefix = "wcl:oauth_token:";
    private const string GraphQlCacheKeyPrefix = "wcl:gql:";
    private const int TokenRefreshBufferSeconds = 60;

    public async Task<string> GetClientTokenAsync(string authUrl)
    {
        var tokenCacheKey = BuildTokenCacheKey(authUrl);

        if (_cache.TryGetValue(tokenCacheKey, out object? cachedToken) && cachedToken is string token)
        {
            _logger.LogDebug("Using cached WarcraftLogs token");
            return token;
        }

        _logger.LogInformation("Requesting new WarcraftLogs OAuth token");
        return await RequestNewTokenAsync(authUrl, tokenCacheKey);
    }

    private async Task<string> RequestNewTokenAsync(string authUrl, string tokenCacheKey)
    {
        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException(
                "WarcraftLogs client credentials not configured. " +
                "Set WarcraftLogs:ClientId and WarcraftLogs:ClientSecret in configuration.");
        }

        var request = new HttpRequestMessage(HttpMethod.Post, authUrl)
        {
            Content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", _options.ClientId),
                new KeyValuePair<string, string>("client_secret", _options.ClientSecret),
            ])
        };

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        if (!root.TryGetProperty("access_token", out var tokenElement))
        {
            throw new InvalidOperationException("No access_token in WarcraftLogs response");
        }

        var token = tokenElement.GetString();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("WarcraftLogs access_token is empty");
        }

        var expiresIn = root.TryGetProperty("expires_in", out var expiresElement)
            ? expiresElement.GetInt32()
            : 3600;

        var cacheExpiry = TimeSpan.FromSeconds(Math.Max(expiresIn - TokenRefreshBufferSeconds, 60));
        _cache.Set(tokenCacheKey, token, cacheExpiry);

        _logger.LogInformation("New WarcraftLogs token obtained, expires in {ExpiresIn} seconds", expiresIn);
        return token;
    }

    public async Task<JsonElement> QueryGraphQLAsync(
        string query,
        object? variables = null,
        string? edition = null,
        bool bypassCache = false)
    {
        var endpoint = _options.ResolveEndpoint(edition);
        var graphQlCacheKey = BuildGraphQlCacheKey(endpoint.BaseUrl, query, variables);
        var graphQlCacheDuration = TimeSpan.FromSeconds(Math.Max(60, _options.GraphQlCacheTtlSeconds));

        if (!bypassCache)
        {
            var cachedDataJson = await TryReadGraphQlCacheAsync(graphQlCacheKey, graphQlCacheDuration);
            if (!string.IsNullOrWhiteSpace(cachedDataJson))
            {
                using var cachedDoc = JsonDocument.Parse(cachedDataJson);
                return cachedDoc.RootElement.Clone();
            }
        }

        var token = await GetClientTokenAsync(endpoint.AuthUrl);

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint.BaseUrl);
        request.Headers.Add("Authorization", $"Bearer {token}");

        var payload = new
        {
            query,
            variables = variables ?? new { }
        };

        var jsonContent = JsonSerializer.Serialize(payload);
        request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
        {
            var firstError = errors.EnumerateArray().FirstOrDefault();
            var errorMessage = firstError.ValueKind == JsonValueKind.Object &&
                               firstError.TryGetProperty("message", out var messageElement)
                ? messageElement.GetString()
                : "Unknown GraphQL error";

            _logger.LogError("GraphQL error from WarcraftLogs: {Error}", errorMessage);
            throw new InvalidOperationException($"GraphQL error: {errorMessage}");
        }

        if (doc.RootElement.TryGetProperty("data", out var dataElement))
        {
            var dataClone = dataElement.Clone();
            if (!bypassCache)
            {
                await WriteGraphQlCacheAsync(graphQlCacheKey, dataClone.GetRawText());
            }

            return dataClone;
        }

        throw new InvalidOperationException("No data property in WarcraftLogs response");
    }

    public async Task<T> QueryGraphQLAsync<T>(
        string query,
        object? variables = null,
        string? edition = null,
        bool bypassCache = false)
    {
        var data = await QueryGraphQLAsync(query, variables, edition, bypassCache);
        var result = JsonSerializer.Deserialize<T>(data.GetRawText(), new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });

        if (result == null)
        {
            throw new InvalidOperationException($"Failed to deserialize GraphQL response into {typeof(T).Name}.");
        }

        return result;
    }

    public void ClearCache()
    {
        var authUrls = _options.EditionEndpoints.Values
            .Select(endpoint => endpoint.AuthUrl)
            .Append(_options.AuthUrl)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var authUrl in authUrls)
        {
            _cache.Remove(BuildTokenCacheKey(authUrl));
        }

        try
        {
            if (Directory.Exists(_graphQlCacheDirectory))
            {
                var files = Directory.GetFiles(_graphQlCacheDirectory, "*.json");
                foreach (var file in files)
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear WarcraftLogs GraphQL file cache");
        }

        _logger.LogInformation("Cleared WarcraftLogs token cache and GraphQL response cache");
    }

    private static string BuildTokenCacheKey(string authUrl)
    {
        if (Uri.TryCreate(authUrl, UriKind.Absolute, out var uri))
        {
            return $"{TokenCacheKeyPrefix}{uri.Host.ToLowerInvariant()}";
        }

        return $"{TokenCacheKeyPrefix}default";
    }

    private static string BuildGraphQlCacheKey(string endpointBaseUrl, string query, object? variables)
    {
        var variablesJson = variables == null ? "{}" : JsonSerializer.Serialize(variables);
        var raw = $"{endpointBaseUrl}|{query}|{variablesJson}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        return $"{GraphQlCacheKeyPrefix}{hash}";
    }

    private static string InitializeGraphQlCacheDirectory(string contentRootPath)
    {
        var path = Path.Combine(contentRootPath, "Data", "cache", "wcl-graphql");
        Directory.CreateDirectory(path);
        return path;
    }

    private string BuildGraphQlCacheFilePath(string graphQlCacheKey)
    {
        var fileHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(graphQlCacheKey))).ToLowerInvariant();
        return Path.Combine(_graphQlCacheDirectory, $"{fileHash}.json");
    }

    private async Task<string?> TryReadGraphQlCacheAsync(string graphQlCacheKey, TimeSpan maxAge)
    {
        var path = BuildGraphQlCacheFilePath(graphQlCacheKey);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var age = DateTimeOffset.UtcNow - new DateTimeOffset(File.GetLastWriteTimeUtc(path));
            if (age > maxAge)
            {
                File.Delete(path);
                return null;
            }

            return await File.ReadAllTextAsync(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read WarcraftLogs GraphQL cache file {Path}", path);
            return null;
        }
    }

    private async Task WriteGraphQlCacheAsync(string graphQlCacheKey, string payload)
    {
        var path = BuildGraphQlCacheFilePath(graphQlCacheKey);

        try
        {
            await File.WriteAllTextAsync(path, payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write WarcraftLogs GraphQL cache file {Path}", path);
        }
    }
}