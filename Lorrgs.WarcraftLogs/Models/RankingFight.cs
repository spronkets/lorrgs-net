using System.Text.Json.Serialization;

namespace Lorrgs.WarcraftLogs.Models;

/// <summary>
/// A single fight within a ranking report.
/// </summary>
public class RankingFight
{
    public RaidFightRanking? Speed { get; set; }
    public RaidFightRanking? Execution { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CompRankingComposition? Composition { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, int>? RoleCounts { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Deaths { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? DamageTaken { get; set; }
    public int EncounterId { get; set; }
    public IReadOnlyList<TimelinePhase>? Phases { get; set; }
    public int FightId { get; set; }
    public required string Name { get; set; }
    public int Duration { get; set; } // milliseconds
    public bool IsKill { get; set; }
    public DateTime StartTime { get; set; }
    public double? Percentile { get; set; }
}
