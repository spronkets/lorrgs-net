namespace Lorrgs.Api.Models;

/// <summary>
/// Role in WoW (Tank, Heal, Melee DPS, Ranged DPS)
/// </summary>
public class WowRole
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string NameSlug { get; set; } = "";
    public string Code { get; set; } = ""; // "tank", "heal", "mdps", "rdps"
    public string Metric { get; set; } = "dps"; // For rankings: "dps" or "hps"
    public string Color { get; set; } = ""; // For UI display
}

/// <summary>
/// WoW Class (Warrior, Mage, etc.)
/// </summary>
public class WowClass
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string NameSlug { get; set; } = "";
    public string Color { get; set; } = "";
    public int MaskId { get; set; }
}

/// <summary>
/// WoW Spec (Frost Mage, Holy Priest, etc.)
/// </summary>
public class WowSpec
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string NameSlug { get; set; } = "";
    public string FullName { get; set; } = ""; // "Frost Mage"
    public string FullNameSlug { get; set; } = ""; // "frost-mage"
    
    public int ClassId { get; set; }
    public WowClass? WowClass { get; set; }
    
    public int RoleId { get; set; }
    public WowRole? Role { get; set; }
}

/// <summary>
/// Raid boss encounter
/// </summary>
public class RaidBoss
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string NameSlug { get; set; } = "";
    public string FullNameSlug { get; set; } = ""; // Used in URLs
    public string Icon { get; set; } = ""; // Wowhead CDN filename
    
    public int RaidId { get; set; }
    public string DungeonName { get; set; } = "";
    
    public string[] Difficulties { get; set; } = { "Mythic", "Heroic", "Normal" };
    public int Tier { get; set; } // Raid tier (e.g., 31 for current)
}

/// <summary>
/// Season (Expansion/patch period)
/// </summary>
public class Season
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public int[] RaidIds { get; set; } = Array.Empty<int>();
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

/// <summary>
/// Raid zone (e.g., Nerub'ar Palace)
/// </summary>
public class RaidZone
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Icon { get; set; } = "";
    public int[] BossIds { get; set; } = Array.Empty<int>();
}

/// <summary>
/// Spell/ability
/// </summary>
public class WowSpell
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Type { get; set; } = "";
}

/// <summary>
/// Trinket/equippable item
/// </summary>
public class WowTrinket
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public int ItemLevel { get; set; }
}
