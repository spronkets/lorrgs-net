namespace Lorrgs.Api.Features.RaidCatalog.GetCatalog;

public class RaidEdition
{
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public int Order { get; set; }
    public int ActivePhase { get; set; }
}
