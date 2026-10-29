namespace Lorrgs.WarcraftLogs.Models;

/// <summary>
/// Helper functions for ranking data.
/// </summary>
public static class RankingHelpers
{
    /// <summary>
    /// Format duration in milliseconds to human-readable string (e.g., "5m 30s").
    /// </summary>
    public static string FormatDuration(int milliseconds)
    {
        var seconds = milliseconds / 1000;
        var minutes = seconds / 60;
        var remainingSeconds = seconds % 60;

        if (minutes > 0)
            return $"{minutes}m {remainingSeconds}s";
        else
            return $"{remainingSeconds}s";
    }
}
