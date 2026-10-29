namespace Lorrgs.WarcraftLogs.Models;

/// <summary>Warcraft Logs raid-level ranking for a fight's speed or execution.</summary>
public sealed record RaidFightRanking(int? Rank, int? Best, int? TotalParses, double? RankPercent);
