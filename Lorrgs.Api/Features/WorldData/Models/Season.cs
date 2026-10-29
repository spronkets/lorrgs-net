namespace Lorrgs.Api.Features.WorldData.Models;

/// <summary>
/// Season (Expansion/patch period)
/// </summary>
public class Season
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public int[] RaidIds { get; set; } = [];
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
