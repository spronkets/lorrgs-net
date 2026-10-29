using System.Text.Json.Serialization;

namespace Lorrgs.Api.Models;

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

/// <summary>Warcraft Logs raid-level ranking for a fight's speed or execution.</summary>
public sealed record RaidFightRanking(int? Rank, int? Best, int? TotalParses, double? RankPercent);

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

/// <summary>
/// Composition breakdown for a fight (spec and role counts).
/// </summary>
public class CompRankingComposition
{
    public Dictionary<string, int> Specs { get; set; } = [];
    public Dictionary<string, int> Roles { get; set; } = [];
    public Dictionary<string, int> Classes { get; set; } = [];
}

/// <summary>
/// A player in a ranking report.
/// </summary>
public class RankingPlayer
{
    /// <summary>Warcraft Logs parse percentile for the player's role-appropriate metric.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? ParsePercentile { get; set; }
    /// <summary>The player's complete timeline loaded with composition kill details.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ReportTimeline? Timeline { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RoleCode { get; set; }
    public int PlayerId { get; set; }
    public required string Name { get; set; }
    public string? GuildName { get; set; }
    public int? FactionId { get; set; }
    public string? ServerRegion { get; set; }
    public string? ServerName { get; set; }

    // Internal alias: existing mapping code currently resolves slugs.
    [JsonIgnore]
    public string? ServerSlug
    {
        get => ServerName;
        set => ServerName = value;
    }

    public int ClassId { get; set; }
    public int SpecId { get; set; }
    public required string SpecSlug { get; set; }
    public double Performance { get; set; } // DPS or HPS value
    public List<RankingCast> Casts { get; set; } = [];
}

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

/// <summary>
/// Helper functions for ranking data.
/// </summary>
public static class RankingHelpers
{
    /// <summary>
    /// Format duration in milliseconds to human-readable string (e.g., "5m 30s").
    /// </summary>
    public static string FormatDuration(int milliseconds)
    {
        var seconds = milliseconds / 1000;
        var minutes = seconds / 60;
        var remainingSeconds = seconds % 60;
        
        if (minutes > 0)
            return $"{minutes}m {remainingSeconds}s";
        else
            return $"{remainingSeconds}s";
    }
}
