namespace Lorrgs.WarcraftLogs.Models;

/// <summary>
/// Represents ranking data for a specific spec and boss encounter.
/// </summary>
public class SpecRankingData
{
    public required string SpecSlug { get; set; }
    public required string BossSlug { get; set; }
    public required string Difficulty { get; set; }
    public required string Metric { get; set; }
    public string? BossIconUrl { get; set; }
    public int Page { get; set; } = 1;
    public bool HasMorePages { get; set; }
    public int TotalCount { get; set; }
    public List<RankingReport> Reports { get; set; } = [];
    public DateTime Updated { get; set; }
    public bool IsDirty { get; set; }
    public double? Percentile { get; set; }
}
