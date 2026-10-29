namespace Lorrgs.Api.Features.WorldData.Models;

/// <summary>
/// Role in WoW (Tank, Heal, Melee DPS, Ranged DPS)
/// </summary>
public class WowRole
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string NameSlug { get; set; } = "";
    public string Code { get; set; } = ""; // "tank", "heal", "mdps", "rdps"
    public string Metric { get; set; } = "dps"; // For rankings: "dps" or "hps"
    public string Color { get; set; } = ""; // For UI display
}
