namespace Lorrgs.WarcraftLogs.Models;

/// <summary>A cast, buff interval, death, or inferred marker relative to fight start, in milliseconds.</summary>
public sealed record TimelineEvent(int SpellId, double Timestamp, double? Duration, string Kind,
    bool IsInferred = false);
