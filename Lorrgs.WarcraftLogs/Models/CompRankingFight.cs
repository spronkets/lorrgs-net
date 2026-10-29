namespace Lorrgs.WarcraftLogs.Models;

/// <summary>
/// A single fight within a comp ranking report, includes composition info.
/// </summary>
public class CompRankingFight
{
    public int FightId { get; set; }
    public required string Name { get; set; }
    public int Duration { get; set; } // milliseconds
    public bool IsKill { get; set; }
    public DateTime StartTime { get; set; }
    public required CompRankingComposition Composition { get; set; }
    public List<CompRankingPlayer> Players { get; set; } = [];
}
