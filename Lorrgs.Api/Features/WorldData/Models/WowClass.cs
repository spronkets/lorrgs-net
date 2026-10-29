namespace Lorrgs.Api.Features.WorldData.Models;

/// <summary>
/// WoW Class (Warrior, Mage, etc.)
/// </summary>
public class WowClass
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string NameSlug { get; set; } = "";
    public string Color { get; set; } = "";
    public int MaskId { get; set; }
}
