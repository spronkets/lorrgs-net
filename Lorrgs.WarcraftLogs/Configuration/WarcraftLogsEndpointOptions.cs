namespace Lorrgs.WarcraftLogs.Configuration;

public sealed class WarcraftLogsEndpointOptions
{
    public string BaseUrl { get; set; } = string.Empty;

    public string AuthUrl { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(AuthUrl);
}
