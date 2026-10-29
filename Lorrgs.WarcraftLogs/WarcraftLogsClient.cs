using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    private const int GraphQlMaxAttempts = 3;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> GraphQlCacheKeyLocks = new(StringComparer.Ordinal);

    private TimeSpan GraphQlCacheDuration =>
        _options.GraphQlCacheTtl < TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : _options.GraphQlCacheTtl;

    public async Task<string> GetClientTokenAsync(string authUrl, CancellationToken cancellationToken = default)
    {
        var tokenCacheKey = BuildTokenCacheKey(authUrl);

        if (_cache.TryGetValue(tokenCacheKey, out object? cachedToken) && cachedToken is string token)
        {
            _logger.LogDebug("Using cached WarcraftLogs token");
            return token;
        }

        _logger.LogInformation("Requesting new WarcraftLogs OAuth token");
        return await RequestNewTokenAsync(authUrl, tokenCacheKey, cancellationToken);
    }

    private async Task<string> RequestNewTokenAsync(string authUrl, string tokenCacheKey, CancellationToken cancellationToken)
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

        using var response = await SendGraphQlWithRetryAsync(request, cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
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
        bool bypassCache = false,
        CancellationToken cancellationToken = default)
    {
        var endpoint = _options.ResolveEndpoint(edition);

        var graphQlCacheKey = BuildGraphQlCacheKey(endpoint.BaseUrl, query, variables);
        var graphQlCacheDuration = GraphQlCacheDuration;
        var cacheLock = GraphQlCacheKeyLocks.GetOrAdd(graphQlCacheKey, static _ => new SemaphoreSlim(1, 1));
        await cacheLock.WaitAsync(cancellationToken);

        try
        {
            var cachedDataJson = bypassCache
                ? null
                : await TryReadGraphQlCacheAsync(graphQlCacheKey, graphQlCacheDuration);
            if (!string.IsNullOrWhiteSpace(cachedDataJson))
            {
                using var cachedDoc = JsonDocument.Parse(cachedDataJson);
                return cachedDoc.RootElement.Clone();
            }

            cancellationToken.ThrowIfCancellationRequested();
            var data = await ExecuteGraphQlRequestAsync(endpoint, query, variables, cancellationToken);
            await WriteGraphQlCacheAsync(graphQlCacheKey, data.GetRawText());
            return data;
        }
        finally
        {
            cacheLock.Release();
        }
    }

    public async Task<JsonElement?> TryQueryGraphQLFromCacheAsync(
        string query, object? variables = null, string? edition = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = _options.ResolveEndpoint(edition);
        var key = BuildGraphQlCacheKey(endpoint.BaseUrl, query, variables);
        var cacheLock = GraphQlCacheKeyLocks.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        await cacheLock.WaitAsync(cancellationToken);
        try
        {
            var json = await TryReadGraphQlCacheAsync(key, GraphQlCacheDuration);
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(json)) return null;
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        finally
        {
            cacheLock.Release();
        }
    }

    private async Task<JsonElement> ExecuteGraphQlRequestAsync(
        WarcraftLogsEndpointOptions endpoint,
        string query,
        object? variables,
        CancellationToken cancellationToken)
    {
        var token = await GetClientTokenAsync(endpoint.AuthUrl, cancellationToken);

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint.BaseUrl);
        request.Headers.Add("Authorization", $"Bearer {token}");

        var payload = new
        {
            query,
            variables = variables ?? new { }
        };

        var jsonContent = JsonSerializer.Serialize(payload);
        request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        using var response = await SendGraphQlWithRetryAsync(request, cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(content);

        if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array &&
            errors.GetArrayLength() > 0)
        {
            var parsedErrors = errors.EnumerateArray().Select(ParseGraphQlError).ToArray();
            var partialData = doc.RootElement.TryGetProperty("data", out var partialDataElement)
                ? partialDataElement.Clone()
                : (JsonElement?)null;

            foreach (var error in parsedErrors)
                _logger.LogError("GraphQL error from WarcraftLogs: {Error}", error.Message ?? "Unknown GraphQL error");
            throw WarcraftLogsGraphQlException.Create(parsedErrors, partialData);
        }

        if (doc.RootElement.TryGetProperty("data", out var dataElement))
        {
            return dataElement.Clone();
        }

        throw new InvalidOperationException("No data property in WarcraftLogs response");
    }

    private static WarcraftLogsGraphQlError ParseGraphQlError(JsonElement error)
    {
        var message = error.ValueKind == JsonValueKind.Object &&
            error.TryGetProperty("message", out var messageElement) &&
            messageElement.ValueKind == JsonValueKind.String
                ? messageElement.GetString()
                : null;
        var path = error.ValueKind == JsonValueKind.Object &&
            error.TryGetProperty("path", out var pathElement) &&
            pathElement.ValueKind == JsonValueKind.Array
                ? pathElement.EnumerateArray().Select(item => item.Clone()).ToArray()
                : [];
        return new WarcraftLogsGraphQlError(message, path, error.Clone(), ClassifyReportFailure(message, path));
    }

    private static WarcraftLogsReportFailureKind? ClassifyReportFailure(
        string? message, IReadOnlyList<JsonElement> path)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;
        var reportPath = path.Any(segment => segment.ValueKind == JsonValueKind.String &&
            segment.GetString() == "reportData") &&
            path.Any(segment => segment.ValueKind == JsonValueKind.String &&
                (segment.GetString() == "report" || IsReportAlias(segment.GetString())));
        if (!reportPath && !message.Contains("report", StringComparison.OrdinalIgnoreCase)) return null;
        if (ContainsAny(message, "private", "permission", "not authorized", "not allowed",
                "access denied", "not have access", "forbidden"))
            return WarcraftLogsReportFailureKind.Private;
        if (ContainsAny(message, "report not found", "report code not found", "no report found",
                "could not find report", "report does not exist", "unknown report", "report was not found"))
            return WarcraftLogsReportFailureKind.NotFound;
        return null;
    }

    private static bool IsReportAlias(string? value) =>
        value is { Length: > 1 } && value[0] == 'r' && value[1..].All(char.IsAsciiDigit);

    private static bool ContainsAny(string message, params string[] phrases) =>
        phrases.Any(phrase => message.Contains(phrase, StringComparison.OrdinalIgnoreCase));

    public async Task<T> QueryGraphQLAsync<T>(
        string query,
        object? variables = null,
        string? edition = null,
        bool bypassCache = false,
        CancellationToken cancellationToken = default)
    {
        var data = await QueryGraphQLAsync(query, variables, edition, bypassCache, cancellationToken);
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

    private async Task<HttpResponseMessage> SendGraphQlWithRetryAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        var rateLimitCacheKey = $"wcl:rate-limit:{request.RequestUri!.GetLeftPart(UriPartial.Authority)}:{_options.ClientId}";
        for (var attempt = 1; attempt <= GraphQlMaxAttempts; attempt++)
        {
            if (_cache.TryGetValue(rateLimitCacheKey, out DateTimeOffset retryAt) && retryAt > DateTimeOffset.UtcNow)
            {
                _logger.LogWarning("WarcraftLogs requests paused until {RetryAt} after an upstream rate limit", retryAt);
                throw new HttpRequestException(
                    "Warcraft Logs rate limit reached. Please retry after the cooldown.",
                    null,
                    HttpStatusCode.TooManyRequests);
            }

            using var clonedRequest = await CloneHttpRequestAsync(request);
            var response = await _httpClient.SendAsync(clonedRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var cooldown = ResolveRateLimitCooldown(response);
                var resumeAt = DateTimeOffset.UtcNow.Add(cooldown);
                _cache.Set(rateLimitCacheKey, resumeAt, cooldown);
                _logger.LogWarning("WarcraftLogs rate limit reached; pausing requests until {RetryAt}", resumeAt);
                using (response)
                {
                    response.EnsureSuccessStatusCode();
                }
            }

            if (!ShouldRetry(response.StatusCode) || attempt == GraphQlMaxAttempts)
            {
                using (response)
                {
                    response.EnsureSuccessStatusCode();
                }
            }

            var delay = ResolveRetryDelay(response, attempt);
            _logger.LogWarning(
                "WarcraftLogs GraphQL request failed with status {StatusCode}; retrying in {DelayMs}ms (attempt {Attempt}/{MaxAttempts})",
                (int)response.StatusCode,
                (int)delay.TotalMilliseconds,
                attempt,
                GraphQlMaxAttempts);

            response.Dispose();
            await Task.Delay(delay, cancellationToken);
        }

        throw new InvalidOperationException("Failed to execute WarcraftLogs GraphQL request after retries.");
    }

    private static bool ShouldRetry(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.RequestTimeout ||
               statusCode == HttpStatusCode.BadGateway ||
               statusCode == HttpStatusCode.ServiceUnavailable ||
               statusCode == HttpStatusCode.GatewayTimeout;
    }

    private static TimeSpan ResolveRateLimitCooldown(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is TimeSpan delta && delta > TimeSpan.Zero)
        {
            return delta;
        }

        if (retryAfter?.Date is DateTimeOffset date && date > DateTimeOffset.UtcNow)
        {
            return date - DateTimeOffset.UtcNow;
        }

        return TimeSpan.FromMinutes(1);
    }

    private static TimeSpan ResolveRetryDelay(HttpResponseMessage response, int attempt)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is TimeSpan delta && delta > TimeSpan.Zero)
        {
            return CapRetryDelay(delta);
        }

        if (retryAfter?.Date is DateTimeOffset date)
        {
            var fromDate = date - DateTimeOffset.UtcNow;
            if (fromDate > TimeSpan.Zero)
            {
                return CapRetryDelay(fromDate);
            }
        }

        var baseDelayMs = 500;
        var exponentialDelayMs = baseDelayMs * (int)Math.Pow(2, attempt - 1);
        return TimeSpan.FromMilliseconds(Math.Min(exponentialDelayMs, 5000));
    }

    private static TimeSpan CapRetryDelay(TimeSpan delay)
    {
        if (delay < TimeSpan.FromMilliseconds(200))
        {
            return TimeSpan.FromMilliseconds(200);
        }

        if (delay > TimeSpan.FromSeconds(10))
        {
            return TimeSpan.FromSeconds(10);
        }

        return delay;
    }

    private static async Task<HttpRequestMessage> CloneHttpRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content is not null)
        {
            var memory = new MemoryStream();
            await request.Content.CopyToAsync(memory);
            memory.Position = 0;

            var contentClone = new StreamContent(memory);
            foreach (var contentHeader in request.Content.Headers)
            {
                contentClone.Headers.TryAddWithoutValidation(contentHeader.Key, contentHeader.Value);
            }

            clone.Content = contentClone;
        }

        return clone;
    }
}
