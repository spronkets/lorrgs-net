namespace Lorrgs.Api.Models;

public class RaidEdition
{
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public int Order { get; set; }
    public bool Public { get; set; }
}

public class RaidInstance
{
    public int ZoneId { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string Edition { get; set; } = "";
    public int Phase { get; set; }
    public List<RaidBossOption> Bosses { get; set; } = [];
    public bool Public { get; set; }
}

public class RaidBossOption
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public bool Mapped { get; set; }
}

public class RaidCatalog
{
    public List<RaidEdition> Editions { get; set; } = [];
    public Dictionary<string, List<RaidInstance>> Instances { get; set; } = new();
}