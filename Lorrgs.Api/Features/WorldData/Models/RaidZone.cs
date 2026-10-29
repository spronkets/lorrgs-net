namespace Lorrgs.Api.Features.WorldData.Models;

/// <summary>
/// Raid zone (e.g., Nerub'ar Palace)
/// </summary>
public class RaidZone
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Icon { get; set; } = "";
    public int[] BossIds { get; set; } = [];
}
