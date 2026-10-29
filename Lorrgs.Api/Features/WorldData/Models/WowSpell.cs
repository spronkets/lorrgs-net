namespace Lorrgs.Api.Features.WorldData.Models;

/// <summary>
/// Spell/ability
/// </summary>
public class WowSpell
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Type { get; set; } = "";
}
