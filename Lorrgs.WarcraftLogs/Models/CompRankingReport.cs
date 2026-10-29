namespace Lorrgs.WarcraftLogs.Models;

/// <summary>
/// A single report in comp rankings.
/// </summary>
public class CompRankingReport
{
    public required string ReportId { get; set; }
    public required string Title { get; set; }
    public List<CompRankingFight> Fights { get; set; } = [];
    public DateTime StartTime { get; set; }
}
