namespace Lorrgs.WarcraftLogs.Models;

/// <summary>
/// A player in a comp ranking report.
/// </summary>
public class CompRankingPlayer
{
    public int PlayerId { get; set; }
    public required string Name { get; set; }
    public int ClassId { get; set; }
    public int SpecId { get; set; }
    public required string SpecSlug { get; set; }
}
