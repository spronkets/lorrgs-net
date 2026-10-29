namespace Lorrgs.WarcraftLogs.Models;

/// <summary>
/// A single cast/ability usage by a player in a fight.
/// </summary>
public class RankingCast
{
    public int SpellId { get; set; }
    public required string SpellName { get; set; }
    public int Count { get; set; }
    public long TotalTime { get; set; }
    public long TotalDamage { get; set; }
    public double Percentage { get; set; }
}
