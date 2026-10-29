namespace Lorrgs.Api.Features.RaidCatalog;

public sealed class RaidCatalogEditionSeed
{
    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Order { get; set; }

    public bool Public { get; set; } = true;

    public List<RaidCatalogRaidSeed> Raids { get; set; } = [];
}
