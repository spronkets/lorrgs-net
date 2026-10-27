namespace Lorrgs.WarcraftLogs;

/// <summary>Indicates that Warcraft Logs rejected a GraphQL operation.</summary>
public sealed class WarcraftLogsGraphQlException(string message) : InvalidOperationException(message);
