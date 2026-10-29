namespace Lorrgs.Api.Features.WorldData.Models;

/// <summary>
/// Raid boss encounter
/// </summary>
public class RaidBoss
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string NameSlug { get; set; } = "";
    public string FullNameSlug { get; set; } = ""; // Used in URLs
    public string Icon { get; set; } = ""; // Wowhead CDN filename

    public int RaidId { get; set; }
    public string DungeonName { get; set; } = "";

    public string[] Difficulties { get; set; } = { "Mythic", "Heroic", "Normal" };
    public int Tier { get; set; } // Raid tier (e.g., 31 for current)
}
