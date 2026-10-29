using System.Text.Json.Serialization;

namespace Lorrgs.WarcraftLogs.Models;

/// <summary>
/// A single report in spec rankings showing one high-performing run.
/// </summary>
public class RankingReport
{
    public string? GuildName { get; set; }
    public string? ServerName { get; set; }
    public string? ServerRegion { get; set; }
    public required string ReportId { get; set; }
    public required string Title { get; set; }
    public double? Percentile { get; set; }
    public int Duration { get; set; } // milliseconds
    public string DurationDisplay => RankingHelpers.FormatDuration(Duration);
    public DateTime StartTime { get; set; }
    public List<RankingFight> Fights { get; set; } = [];
    public List<RankingPlayer> Players { get; set; } = [];
    /// <summary>A complete timeline available in the API's unexpired file cache.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ReportTimeline? Timeline { get; set; }
    /// <summary>A complete encounter reference available in the API's unexpired file cache.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BossTimeline? BossTimeline { get; set; }
}
