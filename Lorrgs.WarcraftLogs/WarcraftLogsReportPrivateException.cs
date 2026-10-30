using System.Text.Json;

namespace Lorrgs.WarcraftLogs;

public sealed class WarcraftLogsReportPrivateException(
    string message,
    IReadOnlyList<WarcraftLogsGraphQlError>? errors = null,
    JsonElement? partialData = null)
    : WarcraftLogsGraphQlException(message, errors, partialData)
{
    public bool IsArchived => Message.Contains("report has been archived", StringComparison.OrdinalIgnoreCase) ||
        Message.Contains("report is archived", StringComparison.OrdinalIgnoreCase);
}
