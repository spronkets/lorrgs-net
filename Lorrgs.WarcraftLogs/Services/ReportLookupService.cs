using System.Text.Json;

namespace Lorrgs.WarcraftLogs.Services;

public sealed class ReportLookupService(WarcraftLogsClient client)
{
    private static readonly string Query = GraphQlResourceLoader.Load("WarcraftLogs.ReportLookup.graphql");

    public Task<JsonElement> LoadAsync(string reportCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportCode);
        return client.QueryGraphQLAsync(Query, new { code = reportCode.Trim() },
            cancellationToken: cancellationToken);
    }
}
