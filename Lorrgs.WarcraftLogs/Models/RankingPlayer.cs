using System.Text.Json.Serialization;

namespace Lorrgs.WarcraftLogs.Models;

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
