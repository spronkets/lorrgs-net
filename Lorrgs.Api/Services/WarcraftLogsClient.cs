using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Lorrgs.Api.Services;

/// <summary>
/// Client for WarcraftLogs GraphQL API.
/// Handles OAuth2 token management and GraphQL query execution.
/// Tokens are cached in memory with automatic refresh before expiry.
/// </summary>
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

    private const string TokenCacheKey = "wcl:oauth_token";
    private const int TokenRefreshBufferSeconds = 60; // Refresh 1 minute before expiry

    /// <summary>
    /// Get a valid OAuth token, using cached token if available and not expired.
    /// Automatically requests a new token if cache miss or expired.
    /// </summary>
    public async Task<string> GetClientTokenAsync()
    {
        // Try to get cached token
        if (_cache.TryGetValue(TokenCacheKey, out object? cachedToken))
        {
            if (cachedToken is string token)
            {
                _logger.LogDebug("Using cached WarcraftLogs token");
                return token;
            }
        }

        _logger.LogInformation("Requesting new WarcraftLogs OAuth token");
        return await RequestNewTokenAsync();
    }

    /// <summary>
    /// Request a new OAuth token from WarcraftLogs using client credentials.
    /// </summary>
    private async Task<string> RequestNewTokenAsync()
    {
        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException(
                "WarcraftLogs client credentials not configured. " +
                "Set WarcraftLogs:ClientId and WarcraftLogs:ClientSecret in configuration.");
        }

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _options.AuthUrl);
            request.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", _options.ClientId),
                new KeyValuePair<string, string>("client_secret", _options.ClientSecret),
            });

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
            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException("WarcraftLogs access_token is empty");
            }

            var expiresIn = root.TryGetProperty("expires_in", out var expiresElement)
                ? expiresElement.GetInt32()
                : 3600; // Default 1 hour

            // Cache the token, removing it 1 minute before actual expiry
            var cacheExpiry = TimeSpan.FromSeconds(Math.Max(expiresIn - TokenRefreshBufferSeconds, 60));
            _cache.Set(TokenCacheKey, token, cacheExpiry);

            _logger.LogInformation("New WarcraftLogs token obtained, expires in {ExpiresIn} seconds", expiresIn);
            return token;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to obtain WarcraftLogs OAuth token");
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse WarcraftLogs OAuth response");
            throw;
        }
    }

    /// <summary>
    /// Execute a GraphQL query against the WarcraftLogs API.
    /// Automatically adds authorization header with current token.
    /// </summary>
    public async Task<JsonElement> QueryGraphQLAsync(string query, object? variables = null)
    {
        var token = await GetClientTokenAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, _options.BaseUrl);
        request.Headers.Add("Authorization", $"Bearer {token}");

        var payload = new
        {
            query,
            variables = variables ?? new { }
        };

        var jsonContent = JsonSerializer.Serialize(payload);
        request.Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);

            // Check for GraphQL errors
            if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
            {
                var errorMessage = errors.EnumerateArray().FirstOrDefault().GetProperty("message").GetString();
                _logger.LogError("GraphQL error from WarcraftLogs: {Error}", errorMessage);
                throw new InvalidOperationException($"GraphQL error: {errorMessage}");
            }

            // Return the data property
            if (doc.RootElement.TryGetProperty("data", out var dataElement))
            {
                // Clone so callers can safely access after this method disposes JsonDocument.
                return dataElement.Clone();
            }

            throw new InvalidOperationException("No data property in WarcraftLogs response");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error querying WarcraftLogs API");
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse WarcraftLogs API response");
            throw;
        }
    }

    /// <summary>
    /// Clear the cached token (useful for testing or after credential changes).
    /// </summary>
    public void ClearCache()
    {
        _cache.Remove(TokenCacheKey);
        _logger.LogInformation("Cleared WarcraftLogs token cache");
    }
}
