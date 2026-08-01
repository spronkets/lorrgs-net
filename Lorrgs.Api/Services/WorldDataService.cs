using Lorrgs.Api.Models;

namespace Lorrgs.Api.Services;

/// <summary>
/// Manages static WoW game data (roles, classes, specs, bosses, seasons).
/// Loaded once at startup from hardcoded definitions.
/// </summary>
public class WorldDataService
{
    private static readonly Lazy<WorldDataService> _instance = 
        new(() => new WorldDataService());

    public static WorldDataService Instance => _instance.Value;

    // Collections (these are never modified after initial load)
    public List<WowRole> Roles { get; private set; }
    public List<WowClass> Classes { get; private set; }
    public List<WowSpec> Specs { get; private set; }
    public List<RaidBoss> Bosses { get; private set; }
    public List<Season> Seasons { get; private set; }
    public List<WowSpell> Spells { get; private set; }
    public List<WowTrinket> Trinkets { get; private set; }
    public List<RaidZone> Zones { get; private set; }

    private WorldDataService()
    {
        Roles = InitializeRoles();
        Classes = InitializeClasses();
        Specs = InitializeSpecs();
        Bosses = InitializeBosses();
        Seasons = InitializeSeasons();
        Spells = InitializeSpells();
        Trinkets = InitializeTrinkets();
        Zones = InitializeZones();
    }

    private List<WowRole> InitializeRoles()
    {
        return new List<WowRole>
        {
            new WowRole 
            { 
                Id = 1, 
                Name = "Tank", 
                NameSlug = "tank", 
                Code = "tank", 
                Metric = "dps",
                Color = "#0070dd" 
            },
            new WowRole 
            { 
                Id = 2, 
                Name = "Healer", 
                NameSlug = "healer", 
                Code = "heal", 
                Metric = "hps",
                Color = "#1eff00" 
            },
            new WowRole 
            { 
                Id = 3, 
                Name = "Melee DPS", 
                NameSlug = "melee-dps", 
                Code = "mdps", 
                Metric = "dps",
                Color = "#ff8000" 
            },
            new WowRole 
            { 
                Id = 4, 
                Name = "Ranged DPS", 
                NameSlug = "ranged-dps", 
                Code = "rdps", 
                Metric = "dps",
                Color = "#ff80ff" 
            }
        };
    }

    private List<WowClass> InitializeClasses()
    {
        return new List<WowClass>
        {
            new WowClass { Id = 1, Name = "Warrior", NameSlug = "warrior", Color = "#c79c6e", MaskId = 1 },
            new WowClass { Id = 2, Name = "Paladin", NameSlug = "paladin", Color = "#f58cba", MaskId = 2 },
            new WowClass { Id = 3, Name = "Hunter", NameSlug = "hunter", Color = "#abd473", MaskId = 4 },
            new WowClass { Id = 4, Name = "Rogue", NameSlug = "rogue", Color = "#fff569", MaskId = 8 },
            new WowClass { Id = 5, Name = "Priest", NameSlug = "priest", Color = "#ffffff", MaskId = 16 },
            new WowClass { Id = 6, Name = "Death Knight", NameSlug = "death-knight", Color = "#c41e3a", MaskId = 32 },
            new WowClass { Id = 7, Name = "Shaman", NameSlug = "shaman", Color = "#0070dd", MaskId = 64 },
            new WowClass { Id = 8, Name = "Mage", NameSlug = "mage", Color = "#69ccf0", MaskId = 128 },
            new WowClass { Id = 9, Name = "Warlock", NameSlug = "warlock", Color = "#9482ca", MaskId = 256 },
            new WowClass { Id = 10, Name = "Monk", NameSlug = "monk", Color = "#00ff96", MaskId = 512 },
            new WowClass { Id = 11, Name = "Druid", NameSlug = "druid", Color = "#ff7d0a", MaskId = 1024 },
            new WowClass { Id = 12, Name = "Demon Hunter", NameSlug = "demon-hunter", Color = "#a335ee", MaskId = 2048 },
            new WowClass { Id = 13, Name = "Evoker", NameSlug = "evoker", Color = "#33937f", MaskId = 4096 }
        };
    }

    private List<WowSpec> InitializeSpecs()
    {
        var roles = Roles;
        var classes = Classes;
        
        var specs = new List<WowSpec>
        {
            // Warrior
            new WowSpec { Id = 71, Name = "Arms", NameSlug = "arms", FullName = "Arms Warrior", FullNameSlug = "arms-warrior", ClassId = 1, RoleId = 3 },
            new WowSpec { Id = 72, Name = "Fury", NameSlug = "fury", FullName = "Fury Warrior", FullNameSlug = "fury-warrior", ClassId = 1, RoleId = 3 },
            new WowSpec { Id = 73, Name = "Protection", NameSlug = "protection", FullName = "Protection Warrior", FullNameSlug = "protection-warrior", ClassId = 1, RoleId = 1 },
            
            // Paladin
            new WowSpec { Id = 65, Name = "Holy", NameSlug = "holy", FullName = "Holy Paladin", FullNameSlug = "holy-paladin", ClassId = 2, RoleId = 2 },
            new WowSpec { Id = 66, Name = "Protection", NameSlug = "protection", FullName = "Protection Paladin", FullNameSlug = "protection-paladin", ClassId = 2, RoleId = 1 },
            new WowSpec { Id = 70, Name = "Retribution", NameSlug = "retribution", FullName = "Retribution Paladin", FullNameSlug = "retribution-paladin", ClassId = 2, RoleId = 3 },
            
            // Hunter
            new WowSpec { Id = 103, Name = "Beast Mastery", NameSlug = "beast-mastery", FullName = "Beast Mastery Hunter", FullNameSlug = "beast-mastery-hunter", ClassId = 3, RoleId = 4 },
            new WowSpec { Id = 104, Name = "Marksmanship", NameSlug = "marksmanship", FullName = "Marksmanship Hunter", FullNameSlug = "marksmanship-hunter", ClassId = 3, RoleId = 4 },
            new WowSpec { Id = 105, Name = "Survival", NameSlug = "survival", FullName = "Survival Hunter", FullNameSlug = "survival-hunter", ClassId = 3, RoleId = 3 },
            
            // Rogue
            new WowSpec { Id = 181, Name = "Assassination", NameSlug = "assassination", FullName = "Assassination Rogue", FullNameSlug = "assassination-rogue", ClassId = 4, RoleId = 3 },
            new WowSpec { Id = 182, Name = "Combat", NameSlug = "combat", FullName = "Combat Rogue", FullNameSlug = "combat-rogue", ClassId = 4, RoleId = 3 },
            new WowSpec { Id = 183, Name = "Subtlety", NameSlug = "subtlety", FullName = "Subtlety Rogue", FullNameSlug = "subtlety-rogue", ClassId = 4, RoleId = 3 },
            
            // Priest
            new WowSpec { Id = 256, Name = "Discipline", NameSlug = "discipline", FullName = "Discipline Priest", FullNameSlug = "discipline-priest", ClassId = 5, RoleId = 2 },
            new WowSpec { Id = 257, Name = "Holy", NameSlug = "holy", FullName = "Holy Priest", FullNameSlug = "holy-priest", ClassId = 5, RoleId = 2 },
            new WowSpec { Id = 258, Name = "Shadow", NameSlug = "shadow", FullName = "Shadow Priest", FullNameSlug = "shadow-priest", ClassId = 5, RoleId = 4 },
            
            // Death Knight
            new WowSpec { Id = 250, Name = "Blood", NameSlug = "blood", FullName = "Blood Death Knight", FullNameSlug = "blood-death-knight", ClassId = 6, RoleId = 1 },
            new WowSpec { Id = 251, Name = "Frost", NameSlug = "frost", FullName = "Frost Death Knight", FullNameSlug = "frost-death-knight", ClassId = 6, RoleId = 3 },
            new WowSpec { Id = 252, Name = "Unholy", NameSlug = "unholy", FullName = "Unholy Death Knight", FullNameSlug = "unholy-death-knight", ClassId = 6, RoleId = 3 },
            
            // Shaman
            new WowSpec { Id = 262, Name = "Elemental", NameSlug = "elemental", FullName = "Elemental Shaman", FullNameSlug = "elemental-shaman", ClassId = 7, RoleId = 4 },
            new WowSpec { Id = 263, Name = "Enhancement", NameSlug = "enhancement", FullName = "Enhancement Shaman", FullNameSlug = "enhancement-shaman", ClassId = 7, RoleId = 3 },
            new WowSpec { Id = 264, Name = "Restoration", NameSlug = "restoration", FullName = "Restoration Shaman", FullNameSlug = "restoration-shaman", ClassId = 7, RoleId = 2 },
            
            // Mage
            new WowSpec { Id = 62, Name = "Arcane", NameSlug = "arcane", FullName = "Arcane Mage", FullNameSlug = "arcane-mage", ClassId = 8, RoleId = 4 },
            new WowSpec { Id = 63, Name = "Fire", NameSlug = "fire", FullName = "Fire Mage", FullNameSlug = "fire-mage", ClassId = 8, RoleId = 4 },
            new WowSpec { Id = 64, Name = "Frost", NameSlug = "frost", FullName = "Frost Mage", FullNameSlug = "frost-mage", ClassId = 8, RoleId = 4 },
            
            // Warlock
            new WowSpec { Id = 265, Name = "Affliction", NameSlug = "affliction", FullName = "Affliction Warlock", FullNameSlug = "affliction-warlock", ClassId = 9, RoleId = 4 },
            new WowSpec { Id = 266, Name = "Demonology", NameSlug = "demonology", FullName = "Demonology Warlock", FullNameSlug = "demonology-warlock", ClassId = 9, RoleId = 4 },
            new WowSpec { Id = 267, Name = "Destruction", NameSlug = "destruction", FullName = "Destruction Warlock", FullNameSlug = "destruction-warlock", ClassId = 9, RoleId = 4 },
            
            // Monk
            new WowSpec { Id = 268, Name = "Brewmaster", NameSlug = "brewmaster", FullName = "Brewmaster Monk", FullNameSlug = "brewmaster-monk", ClassId = 10, RoleId = 1 },
            new WowSpec { Id = 269, Name = "Windwalker", NameSlug = "windwalker", FullName = "Windwalker Monk", FullNameSlug = "windwalker-monk", ClassId = 10, RoleId = 3 },
            new WowSpec { Id = 270, Name = "Mistweaver", NameSlug = "mistweaver", FullName = "Mistweaver Monk", FullNameSlug = "mistweaver-monk", ClassId = 10, RoleId = 2 },
            
            // Druid
            new WowSpec { Id = 102, Name = "Balance", NameSlug = "balance", FullName = "Balance Druid", FullNameSlug = "balance-druid", ClassId = 11, RoleId = 4 },
            new WowSpec { Id = 103, Name = "Feral", NameSlug = "feral", FullName = "Feral Druid", FullNameSlug = "feral-druid", ClassId = 11, RoleId = 3 },
            new WowSpec { Id = 104, Name = "Guardian", NameSlug = "guardian", FullName = "Guardian Druid", FullNameSlug = "guardian-druid", ClassId = 11, RoleId = 1 },
            new WowSpec { Id = 105, Name = "Restoration", NameSlug = "restoration", FullName = "Restoration Druid", FullNameSlug = "restoration-druid", ClassId = 11, RoleId = 2 },
            
            // Demon Hunter
            new WowSpec { Id = 577, Name = "Havoc", NameSlug = "havoc", FullName = "Havoc Demon Hunter", FullNameSlug = "havoc-demon-hunter", ClassId = 12, RoleId = 3 },
            new WowSpec { Id = 581, Name = "Vengeance", NameSlug = "vengeance", FullName = "Vengeance Demon Hunter", FullNameSlug = "vengeance-demon-hunter", ClassId = 12, RoleId = 1 },
            
            // Evoker
            new WowSpec { Id = 1467, Name = "Devastation", NameSlug = "devastation", FullName = "Devastation Evoker", FullNameSlug = "devastation-evoker", ClassId = 13, RoleId = 4 },
            new WowSpec { Id = 1468, Name = "Preservation", NameSlug = "preservation", FullName = "Preservation Evoker", FullNameSlug = "preservation-evoker", ClassId = 13, RoleId = 2 },
            new WowSpec { Id = 1473, Name = "Augmentation", NameSlug = "augmentation", FullName = "Augmentation Evoker", FullNameSlug = "augmentation-evoker", ClassId = 13, RoleId = 4 },
        };

        // Link role and class objects
        foreach (var spec in specs)
        {
            spec.Role = roles.FirstOrDefault(r => r.Id == spec.RoleId);
            spec.WowClass = classes.FirstOrDefault(c => c.Id == spec.ClassId);
        }

        return specs;
    }

    private List<RaidBoss> InitializeBosses()
    {
        return new List<RaidBoss>
        {
            // The War Within - Season 1 (Nerub'ar Palace, zone 38)
            new RaidBoss { Id = 2898, Name = "Eranog", NameSlug = "eranog", Icon = "inv_achievement_raidnerubar_eranog.jpg", RaidId = 38 },
            new RaidBoss { Id = 2893, Name = "Terros", NameSlug = "terros", Icon = "inv_achievement_raidnerubar_terros.jpg", RaidId = 38 },
            new RaidBoss { Id = 2897, Name = "Zul'phix", NameSlug = "zul-phix", Icon = "inv_achievement_raidnerubar_zulumar.jpg", RaidId = 38 },
            new RaidBoss { Id = 2914, Name = "Magmorax", NameSlug = "magmorax", Icon = "inv_achievement_raidnerubar_magmorax.jpg", RaidId = 38 },
            new RaidBoss { Id = 2915, Name = "Neltharion", NameSlug = "neltharion", Icon = "inv_achievement_raidnerubar_neltharion.jpg", RaidId = 38 },
            new RaidBoss { Id = 2916, Name = "The Amalgamation", NameSlug = "the-amalgamation", Icon = "inv_achievement_raidnerubar_amalgamation.jpg", RaidId = 38 },
            new RaidBoss { Id = 2917, Name = "Sarkareth, the Shadowbringer", NameSlug = "sarkareth", Icon = "inv_achievement_raidnerubar_sarkareth.jpg", RaidId = 38 },

            // Dragonflight - Season 3 (Aberrus, the Shadowed Crucible, zone 29)
            new RaidBoss { Id = 2680, Name = "Rashok, the Elder", NameSlug = "rashok", Icon = "inv_achievement_raiddragon_rashok.jpg", RaidId = 29 },
            new RaidBoss { Id = 2879, Name = "Kazzara, the Hellreaper", NameSlug = "kazzara", Icon = "inv_achievement_raiddragon_kazzara.jpg", RaidId = 29 },
            new RaidBoss { Id = 2881, Name = "Amalgamation Chamber", NameSlug = "amalgamation-chamber", Icon = "inv_achievement_raiddragon_amalgamation.jpg", RaidId = 29 },
            new RaidBoss { Id = 2896, Name = "Forgotten Experiments", NameSlug = "forgotten-experiments", Icon = "inv_achievement_raiddragon_experiments.jpg", RaidId = 29 },
            new RaidBoss { Id = 2889, Name = "Assault of the Zaqali", NameSlug = "assault-of-the-zaqali", Icon = "inv_achievement_raiddragon_zaqali.jpg", RaidId = 29 },
            new RaidBoss { Id = 2900, Name = "Zskarn", NameSlug = "zskarn", Icon = "inv_achievement_raiddragon_zskarn.jpg", RaidId = 29 },

            // Dragonflight - Season 2 (Vault of the Incarnates, zone 26)
            new RaidBoss { Id = 2519, Name = "Eranog", NameSlug = "eranog-vault", Icon = "inv_achievement_raiddragon_raszageth.jpg", RaidId = 26 },
            new RaidBoss { Id = 2520, Name = "Terros", NameSlug = "terros-vault", Icon = "inv_achievement_raiddragon_terros.jpg", RaidId = 26 },
            new RaidBoss { Id = 2522, Name = "The Primal Council", NameSlug = "primal-council", Icon = "inv_achievement_raiddragon_primialcouncil.jpg", RaidId = 26 },
            new RaidBoss { Id = 2524, Name = "Sennarth, the Cold Breath", NameSlug = "sennarth", Icon = "inv_achievement_raiddragon_sennarth.jpg", RaidId = 26 },
            new RaidBoss { Id = 2526, Name = "Dathea, Ascended", NameSlug = "dathea", Icon = "inv_achievement_raiddragon_dathea.jpg", RaidId = 26 },
            new RaidBoss { Id = 2527, Name = "Raszageth the Storm-Eater", NameSlug = "raszageth", Icon = "inv_achievement_raiddragon_raszageth.jpg", RaidId = 26 }
        };
    }

    private List<Season> InitializeSeasons()
    {
        return new List<Season>
        {
            new Season
            {
                Id = 1,
                Name = "The War Within - Season 1",
                Slug = "tww-s1",
                RaidIds = new int[] { /* TODO: populate from raid data */ },
                StartDate = new DateTime(2024, 9, 10),
                EndDate = null
            }
        };
    }

    private List<WowSpell> InitializeSpells()
    {
        return new List<WowSpell>
        {
            // Sample WoW spells - extend as needed
            new WowSpell { Id = 1, Name = "Fireball", Icon = "spell_fire_fireball", Type = "Damage" },
            new WowSpell { Id = 2, Name = "Frostbolt", Icon = "spell_frost_frostbolt", Type = "Damage" },
            new WowSpell { Id = 3, Name = "Holy Light", Icon = "spell_holy_holylight", Type = "Heal" },
            new WowSpell { Id = 4, Name = "Shadow Bolt", Icon = "spell_shadow_shadowbolt", Type = "Damage" },
            new WowSpell { Id = 5, Name = "Chain Heal", Icon = "spell_nature_chainheal", Type = "Heal" },
            new WowSpell { Id = 6, Name = "Power Word: Radiance", Icon = "spell_holy_powerwordsolace", Type = "Heal" },
            new WowSpell { Id = 7, Name = "Devastation", Icon = "spell_mage_ignite", Type = "Damage" },
            new WowSpell { Id = 8, Name = "Sinful Delight", Icon = "spell_warlock_demonic", Type = "Damage" }
        };
    }

    private List<WowTrinket> InitializeTrinkets()
    {
        return new List<WowTrinket>
        {
            // Sample WoW trinkets - extend as needed
            new WowTrinket { Id = 1, Name = "Alchemy Stone", Icon = "inv_jewelry_trinket_14", ItemLevel = 639 },
            new WowTrinket { Id = 2, Name = "Spymaster's Web", Icon = "inv_jewelry_trinket_40", ItemLevel = 639 },
            new WowTrinket { Id = 3, Name = "Beacon to the Beyond", Icon = "inv_jewelry_trinket_48", ItemLevel = 639 },
            new WowTrinket { Id = 4, Name = "Darkmoon Deck: Wither", Icon = "inv_jewelry_trinket_41", ItemLevel = 639 },
            new WowTrinket { Id = 5, Name = "Vessel of Searing Shadow", Icon = "inv_jewelry_trinket_28", ItemLevel = 639 },
            new WowTrinket { Id = 6, Name = "Broodkeeper's Legacy", Icon = "inv_jewelry_trinket_36", ItemLevel = 639 }
        };
    }

    /// <summary>
    /// Get a spec by its full name slug (e.g., "frost-mage")
    /// </summary>
    public WowSpec? GetSpec(string fullNameSlug)
    {
        return Specs.FirstOrDefault(s => s.FullNameSlug == fullNameSlug);
    }

    /// <summary>
    /// Get all specs for a class
    /// </summary>
    public List<WowSpec> GetSpecsByClass(int classId)
    {
        return Specs.Where(s => s.ClassId == classId).ToList();
    }

    /// <summary>
    /// Get all specs for a role
    /// </summary>
    public List<WowSpec> GetSpecsByRole(int roleId)
    {
        return Specs.Where(s => s.RoleId == roleId).ToList();
    }

    /// <summary>
    /// Get a class by ID
    /// </summary>
    public WowClass? GetClass(int classId)
    {
        return Classes.FirstOrDefault(c => c.Id == classId);
    }

    /// <summary>
    /// Get a class by name slug
    /// </summary>
    public WowClass? GetClass(string nameSlug)
    {
        return Classes.FirstOrDefault(c => c.NameSlug == nameSlug);
    }

    /// <summary>
    /// Get a boss by name slug
    /// </summary>
    public RaidBoss? GetBoss(string nameSlug)
    {
        return Bosses.FirstOrDefault(b => b.NameSlug == nameSlug);
    }

    /// <summary>
    /// Get a boss by ID
    /// </summary>
    public RaidBoss? GetBoss(int bossId)
    {
        return Bosses.FirstOrDefault(b => b.Id == bossId);
    }

    /// <summary>
    /// Get a spell by ID
    /// </summary>
    public WowSpell? GetSpell(int spellId)
    {
        return Spells.FirstOrDefault(s => s.Id == spellId);
    }

    /// <summary>
    /// Get a trinket by ID
    /// </summary>
    public WowTrinket? GetTrinket(int trinketId)
    {
        return Trinkets.FirstOrDefault(t => t.Id == trinketId);
    }

    /// <summary>
    /// Get a season by slug
    /// </summary>
    public Season? GetSeason(string slug)
    {
        return Seasons.FirstOrDefault(s => s.Slug == slug);
    }

    /// <summary>
    /// Get a zone by slug
    /// </summary>
    public RaidZone? GetZone(string slug)
    {
        return Zones.FirstOrDefault(z => z.Slug == slug);
    }

    private List<RaidZone> InitializeZones()
    {
        return new List<RaidZone>
        {
            new RaidZone
            {
                Id = 38,
                Name = "Nerub'ar Palace",
                Slug = "nerubar-palace",
                Icon = "inv_achievement_raidnerubar_sarkareth",
                BossIds = Bosses.Where(b => b.RaidId == 38).Select(b => b.Id).ToArray()
            },
            new RaidZone
            {
                Id = 29,
                Name = "Aberrus, the Shadowed Crucible",
                Slug = "aberrus",
                Icon = "inv_achievement_raiddragon_kazzara",
                BossIds = Bosses.Where(b => b.RaidId == 29).Select(b => b.Id).ToArray()
            },
            new RaidZone
            {
                Id = 26,
                Name = "Vault of the Incarnates",
                Slug = "vault-of-the-incarnates",
                Icon = "inv_achievement_raiddragon_raszageth",
                BossIds = Bosses.Where(b => b.RaidId == 26).Select(b => b.Id).ToArray()
            }
        };
    }
}
