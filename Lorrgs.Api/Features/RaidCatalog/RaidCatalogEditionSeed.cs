namespace Lorrgs.Api.Features.RaidCatalog;

public sealed class RaidCatalogEditionSeed
{
    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Order { get; set; }

    public int ActivePhase { get; set; }

    public List<RaidCatalogRaidSeed> Raids { get; set; } = [];
}
