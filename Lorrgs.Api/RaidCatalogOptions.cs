using Lorrgs.Api.Models;

public sealed class RaidCatalogOptions
{
    public List<RaidCatalogEditionSeed> Editions { get; set; } = [];
}

public sealed class RaidCatalogEditionSeed
{
    public int Id { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Order { get; set; }

    public bool Public { get; set; } = true;

    public List<RaidCatalogRaidSeed> Raids { get; set; } = [];
}

public sealed class RaidCatalogRaidSeed
{
    public int Id { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Phase { get; set; }

    public bool Public { get; set; } = true;

    public string[] Bosses { get; set; } = [];
}
