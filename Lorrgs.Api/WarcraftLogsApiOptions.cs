public sealed class WarcraftLogsApiOptions
{
    public const string SectionName = "WarcraftLogs";

    public string BaseUrl { get; set; } = "https://www.warcraftlogs.com/api/v2/client";

    public string AuthUrl { get; set; } = "https://www.warcraftlogs.com/oauth/token";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public bool HasClientCredentials => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    public bool IsConfigured => HasClientCredentials;
}
