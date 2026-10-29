namespace Lorrgs.WarcraftLogs.Models;

/// <summary>
/// Represents ranking data for compositions (group setups) for a boss.
/// </summary>
public class CompRankingData
{
    public required string BossSlug { get; set; }
    public List<RankingReport> Reports { get; set; } = [];
    public string Difficulty { get; set; } = "";
    public int Page { get; set; } = 1;
    public bool HasMorePages { get; set; }
    public int TotalCount { get; set; }
    public DateTime Updated { get; set; }
}
