namespace Lorrgs.WarcraftLogs.Models;

/// <summary>
/// Composition breakdown for a fight (spec and role counts).
/// </summary>
public class CompRankingComposition
{
    public Dictionary<string, int> Specs { get; set; } = [];
    public Dictionary<string, int> Roles { get; set; } = [];
    public Dictionary<string, int> Classes { get; set; } = [];
}
