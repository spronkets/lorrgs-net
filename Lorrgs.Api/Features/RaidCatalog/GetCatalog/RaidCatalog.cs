namespace Lorrgs.Api.Features.RaidCatalog.GetCatalog;

public class RaidCatalog
{
    public List<RaidEdition> Editions { get; set; } = [];
    public Dictionary<string, List<RaidInstance>> Instances { get; set; } = [];
}
