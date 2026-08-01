using System.Text;
using System.Text.Json;
using Lorrgs.WarcraftLogs.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lorrgs.WarcraftLogs;

public class WarcraftLogsClient(
    ILogger<WarcraftLogsClient> logger,
    IOptions<WarcraftLogsApiOptions> options,
    HttpClient httpClient,
    IMemoryCache cache)
{
    private readonly ILogger<WarcraftLogsClient> _logger = logger;
    private readonly WarcraftLogsApiOptions _options = options.Value;
    private readonly HttpClient _httpClient = httpClient;
    private readonly IMemoryCache _cache = cache;

    private const string TokenCacheKeyPrefix = "wcl:oauth_token:";
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
        string? edition = null)
    {
        var endpoint = _options.ResolveEndpoint(edition);
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
            return dataElement.Clone();
        }

        throw new InvalidOperationException("No data property in WarcraftLogs response");
    }

    public async Task<T> QueryGraphQLAsync<T>(
        string query,
        object? variables = null,
        string? edition = null)
    {
        var data = await QueryGraphQLAsync(query, variables, edition);
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

        _logger.LogInformation("Cleared WarcraftLogs token cache");
    }

    private static string BuildTokenCacheKey(string authUrl)
    {
        if (Uri.TryCreate(authUrl, UriKind.Absolute, out var uri))
        {
            return $"{TokenCacheKeyPrefix}{uri.Host.ToLowerInvariant()}";
        }

        return $"{TokenCacheKeyPrefix}default";
    }
}