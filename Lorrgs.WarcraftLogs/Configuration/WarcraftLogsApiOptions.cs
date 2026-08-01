namespace Lorrgs.WarcraftLogs.Configuration;

public sealed class WarcraftLogsApiOptions
{
    public const string SectionName = "WarcraftLogs";

    public string BaseUrl { get; set; } = "https://www.warcraftlogs.com/api/v2/client";

    public string AuthUrl { get; set; } = "https://www.warcraftlogs.com/oauth/token";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public Dictionary<string, WarcraftLogsEndpointOptions> EditionEndpoints { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public bool HasClientCredentials => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    public bool IsConfigured => HasClientCredentials;

    public WarcraftLogsEndpointOptions ResolveEndpoint(string? edition)
    {
        var defaultEndpoint = new WarcraftLogsEndpointOptions
        {
            BaseUrl = BaseUrl,
            AuthUrl = AuthUrl
        };

        if (string.IsNullOrWhiteSpace(edition) || EditionEndpoints.Count == 0)
        {
            return defaultEndpoint;
        }

        var normalizedKey = NormalizeEditionKey(edition);
        if (EditionEndpoints.TryGetValue(normalizedKey, out var configured) && configured.IsConfigured)
        {
            return configured;
        }

        var trimmedEdition = edition.Trim();
        if (EditionEndpoints.TryGetValue(trimmedEdition, out configured) && configured.IsConfigured)
        {
            return configured;
        }

        return defaultEndpoint;
    }

    private static string NormalizeEditionKey(string edition)
    {
        var key = string.Concat(edition.Where(char.IsLetterOrDigit)).ToLowerInvariant();

        return key switch
        {
            "anniversary" => "anniversary",
            "mistsofpandaria" => "mistsofpandaria",
            "era" => "era",
            "retail" => "retail",
            _ => key
        };
    }
}

public sealed class WarcraftLogsEndpointOptions
{
    public string BaseUrl { get; set; } = string.Empty;

    public string AuthUrl { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(AuthUrl);
}