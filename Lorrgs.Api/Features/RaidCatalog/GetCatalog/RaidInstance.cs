namespace Lorrgs.Api.Features.RaidCatalog.GetCatalog;

public class RaidInstance
{
    public string? RaidSize { get; set; }
    public int ZoneId { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string Edition { get; set; } = "";
    public int Phase { get; set; }
    public List<RaidBossOption> Bosses { get; set; } = [];
    public bool IsAvailable { get; set; }
}
