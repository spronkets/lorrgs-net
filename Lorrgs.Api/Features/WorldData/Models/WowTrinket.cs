namespace Lorrgs.Api.Features.WorldData.Models;

/// <summary>
/// Trinket/equippable item
/// </summary>
public class WowTrinket
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public int ItemLevel { get; set; }
}
