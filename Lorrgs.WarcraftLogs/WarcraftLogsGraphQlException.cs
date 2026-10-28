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

public enum WarcraftLogsReportFailureKind
{
    Private,
    NotFound
}

/// <summary>Indicates that Warcraft Logs rejected a GraphQL operation.</summary>
public class WarcraftLogsGraphQlException : InvalidOperationException
{
    public WarcraftLogsGraphQlException(string message,
        IReadOnlyList<WarcraftLogsGraphQlError>? errors = null, JsonElement? partialData = null)
        : base(message)
    {
        Errors = errors ?? [];
        PartialData = partialData;
    }

    public IReadOnlyList<WarcraftLogsGraphQlError> Errors { get; }
    public JsonElement? PartialData { get; }

    public bool IsReportAccessFailure =>
        Errors.Count > 0 && Errors.All(error => error.ReportFailure.HasValue);

    public IReadOnlySet<string> InaccessibleReportAliases => Errors
        .Where(error => error.ReportFailure.HasValue && error.ReportAlias is not null)
        .Select(error => error.ReportAlias!)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static WarcraftLogsGraphQlException Create(
        IReadOnlyList<WarcraftLogsGraphQlError> errors, JsonElement? partialData)
    {
        var firstMessage = errors.FirstOrDefault()?.Message ?? "Unknown GraphQL error";
        var message = $"GraphQL error: {firstMessage}";
        if (errors.Count > 0 && errors.All(error => error.ReportFailure == WarcraftLogsReportFailureKind.Private))
            return new WarcraftLogsReportPrivateException(message, errors, partialData);
        if (errors.Count > 0 && errors.All(error => error.ReportFailure == WarcraftLogsReportFailureKind.NotFound))
            return new WarcraftLogsReportNotFoundException(message, errors, partialData);
        return new WarcraftLogsGraphQlException(message, errors, partialData);
    }
}

public sealed class WarcraftLogsReportPrivateException(
    string message,
    IReadOnlyList<WarcraftLogsGraphQlError>? errors = null,
    JsonElement? partialData = null)
    : WarcraftLogsGraphQlException(message, errors, partialData);

public sealed class WarcraftLogsReportNotFoundException(
    string message,
    IReadOnlyList<WarcraftLogsGraphQlError>? errors = null,
    JsonElement? partialData = null)
    : WarcraftLogsGraphQlException(message, errors, partialData);
