namespace Lorrgs.Api.Features.RaidCatalog.GetCatalog;

public class RaidBossOption
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public bool Mapped { get; set; }
}
