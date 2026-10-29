using System.Text.Json;

namespace Lorrgs.WarcraftLogs;

/// <summary>One error reported by the Warcraft Logs GraphQL API.</summary>
public sealed record WarcraftLogsGraphQlError(
    string? Message,
    IReadOnlyList<JsonElement> Path,
    JsonElement Details,
    WarcraftLogsReportFailureKind? ReportFailure)
{
    public string? ReportAlias => Path
        .Where(segment => segment.ValueKind == JsonValueKind.String)
        .Select(segment => segment.GetString())
        .FirstOrDefault(segment => segment is not null &&
            segment.Length > 1 && segment[0] == 'r' && segment[1..].All(char.IsAsciiDigit));
}
