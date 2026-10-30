namespace Lorrgs.Api.Features.RaidCatalog;

public sealed class RaidCatalogRaidSeed
{
    public string? RaidSize { get; set; }
    public int ZoneId { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Phase { get; set; }

    public string[] Bosses { get; set; } = [];
}
