using Microsoft.AspNetCore.Mvc;
using Lorrgs.Api.Models;
using Lorrgs.Api.Services;

namespace Lorrgs.Api.Controllers;

/// <summary>
/// Provides static World of Warcraft game data: roles, classes, specs, bosses, etc.
/// Data is loaded from hardcoded definitions and cached for performance.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class WorldDataController(
    ILogger<WorldDataController> logger,
    CacheService cacheService) : ControllerBase
{
    private readonly ILogger<WorldDataController> _logger = logger;
    private readonly WorldDataService _worldDataService = WorldDataService.Instance;
    private readonly CacheService _cacheService = cacheService;

    /// <summary>
    /// Get all WoW roles (Tank, Healer, Melee DPS, Ranged DPS)
    /// </summary>
    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles()
    {
        _logger.LogInformation("Fetching all roles");
        
        var cached = await _cacheService.GetAsync<List<WowRole>>("world-data", "roles");
        if (cached != null)
        {
            return Ok(new { roles = cached });
        }

        var roles = _worldDataService.Roles.OrderBy(r => r.Id).ToList();
        await _cacheService.SetAsync("world-data", "roles", roles);

        return Ok(new { roles });
    }

    /// <summary>
    /// Get all WoW classes (Warrior, Mage, etc.)
    /// </summary>
    [HttpGet("classes")]
    public async Task<IActionResult> GetClasses()
    {
        _logger.LogInformation("Fetching all classes");

        var cached = await _cacheService.GetAsync<Dictionary<string, WowClass>>("world-data", "classes");
        if (cached != null)
        {
            return Ok(cached);
        }

        // Return as dictionary keyed by name slug
        var classes = _worldDataService.Classes
            .OrderBy(c => c.Name)
            .ToDictionary(c => c.NameSlug);

        await _cacheService.SetAsync("world-data", "classes", classes);

        return Ok(classes);
    }

    /// <summary>
    /// Get all WoW specs, optionally filtered by role or class
    /// </summary>
    [HttpGet("specs")]
    public async Task<IActionResult> GetSpecs([FromQuery] int? roleId = null, [FromQuery] int? classId = null)
    {
        _logger.LogInformation("Fetching specs: roleId={RoleId}, classId={ClassId}", roleId, classId);

        var cacheKey = $"specs_{roleId ?? 0}_{classId ?? 0}";
        var cached = await _cacheService.GetAsync<List<WowSpec>>("world-data", cacheKey);
        if (cached != null)
        {
            return Ok(new { specs = cached });
        }

        var specs = _worldDataService.Specs.AsEnumerable();

        if (roleId.HasValue)
            specs = specs.Where(s => s.RoleId == roleId);

        if (classId.HasValue)
            specs = specs.Where(s => s.ClassId == classId);

        var result = specs.OrderBy(s => s.FullName).ToList();
        await _cacheService.SetAsync("world-data", cacheKey, result);

        return Ok(new { specs = result });
    }

    /// <summary>
    /// Get a specific spec by its slug (e.g., "frost-mage")
    /// </summary>
    [HttpGet("specs/{specSlug}")]
    public async Task<IActionResult> GetSpec(string specSlug)
    {
        _logger.LogInformation("Fetching spec: {SpecSlug}", specSlug);

        var cached = await _cacheService.GetAsync<WowSpec>("world-data", $"spec_{specSlug}");
        if (cached != null)
        {
            return Ok(cached);
        }

        var spec = _worldDataService.GetSpec(specSlug);
        if (spec == null)
        {
            return NotFound(new { error = $"Spec not found: {specSlug}" });
        }

        await _cacheService.SetAsync("world-data", $"spec_{specSlug}", spec);

        return Ok(spec);
    }

    /// <summary>
    /// Get all specs for a specific class
    /// </summary>
    [HttpGet("classes/{classSlug}/specs")]
    public async Task<IActionResult> GetClassSpecs(string classSlug)
    {
        _logger.LogInformation("Fetching specs for class: {ClassSlug}", classSlug);

        var cacheKey = $"class_specs_{classSlug}";
        var cached = await _cacheService.GetAsync<List<WowSpec>>("world-data", cacheKey);
        if (cached != null)
        {
            return Ok(new { specs = cached });
        }

        var wowClass = _worldDataService.Classes.FirstOrDefault(c => c.NameSlug == classSlug);
        if (wowClass == null)
        {
            return NotFound(new { error = $"Class not found: {classSlug}" });
        }

        var specs = _worldDataService.GetSpecsByClass(wowClass.Id)
            .OrderBy(s => s.Name)
            .ToList();

        await _cacheService.SetAsync("world-data", cacheKey, specs);

        return Ok(new { specs });
    }

    /// <summary>
    /// Get all specs for a specific role
    /// </summary>
    [HttpGet("roles/{roleSlug}/specs")]
    public async Task<IActionResult> GetRoleSpecs(string roleSlug)
    {
        _logger.LogInformation("Fetching specs for role: {RoleSlug}", roleSlug);

        var cacheKey = $"role_specs_{roleSlug}";
        var cached = await _cacheService.GetAsync<List<WowSpec>>("world-data", cacheKey);
        if (cached != null)
        {
            return Ok(new { specs = cached });
        }

        var role = _worldDataService.Roles.FirstOrDefault(r => r.NameSlug == roleSlug);
        if (role == null)
        {
            return NotFound(new { error = $"Role not found: {roleSlug}" });
        }

        var specs = _worldDataService.GetSpecsByRole(role.Id)
            .OrderBy(s => s.FullName)
            .ToList();

        await _cacheService.SetAsync("world-data", cacheKey, specs);

        return Ok(new { specs });
    }

    /// <summary>
    /// Get all raid bosses
    /// </summary>
    [HttpGet("bosses")]
    public async Task<IActionResult> GetBosses()
    {
        _logger.LogInformation("Fetching all bosses");

        var cached = await _cacheService.GetAsync<List<RaidBoss>>("world-data", "bosses");
        if (cached != null)
        {
            return Ok(new { bosses = cached });
        }

        var bosses = _worldDataService.Bosses.OrderBy(b => b.Name).ToList();
        await _cacheService.SetAsync("world-data", "bosses", bosses);

        return Ok(new { bosses });
    }

    /// <summary>
    /// Get a specific boss by slug
    /// </summary>
    [HttpGet("bosses/{bossSlug}")]
    public async Task<IActionResult> GetBoss(string bossSlug)
    {
        _logger.LogInformation("Fetching boss: {BossSlug}", bossSlug);

        var cached = await _cacheService.GetAsync<RaidBoss>("world-data", $"boss_{bossSlug}");
        if (cached != null)
        {
            return Ok(cached);
        }

        var boss = _worldDataService.Bosses.FirstOrDefault(b => b.NameSlug == bossSlug);
        if (boss == null)
        {
            return NotFound(new { error = $"Boss not found: {bossSlug}" });
        }

        await _cacheService.SetAsync("world-data", $"boss_{bossSlug}", boss);

        return Ok(boss);
    }

    /// <summary>
    /// Get all raid zones
    /// </summary>
    [HttpGet("zones")]
    public async Task<IActionResult> GetZones()
    {
        _logger.LogInformation("Fetching all zones");

        var cached = await _cacheService.GetAsync<List<RaidZone>>("world-data", "zones");
        if (cached != null)
        {
            return Ok(new { zones = cached });
        }

        var zones = _worldDataService.Zones.OrderByDescending(z => z.Id).ToList();
        await _cacheService.SetAsync("world-data", "zones", zones);

        return Ok(new { zones });
    }

    /// <summary>
    /// Get a specific zone by slug
    /// </summary>
    [HttpGet("zones/{zoneSlug}")]
    public async Task<IActionResult> GetZone(string zoneSlug)
    {
        _logger.LogInformation("Fetching zone: {ZoneSlug}", zoneSlug);

        var cached = await _cacheService.GetAsync<RaidZone>("world-data", $"zone_{zoneSlug}");
        if (cached != null)
        {
            return Ok(cached);
        }

        var zone = _worldDataService.GetZone(zoneSlug);
        if (zone == null)
        {
            return NotFound(new { error = $"Zone not found: {zoneSlug}" });
        }

        await _cacheService.SetAsync("world-data", $"zone_{zoneSlug}", zone);

        return Ok(zone);
    }

    /// <summary>
    /// Get all bosses in a zone
    /// </summary>
    [HttpGet("zones/{zoneSlug}/bosses")]
    public async Task<IActionResult> GetZoneBosses(string zoneSlug)
    {
        _logger.LogInformation("Fetching bosses for zone: {ZoneSlug}", zoneSlug);

        var cacheKey = $"zone_bosses_{zoneSlug}";
        var cached = await _cacheService.GetAsync<List<RaidBoss>>("world-data", cacheKey);
        if (cached != null)
        {
            return Ok(new { bosses = cached });
        }

        var zone = _worldDataService.GetZone(zoneSlug);
        if (zone == null)
        {
            return NotFound(new { error = $"Zone not found: {zoneSlug}" });
        }

        var bosses = _worldDataService.Bosses
            .Where(b => b.RaidId == zone.Id)
            .ToList();

        await _cacheService.SetAsync("world-data", cacheKey, bosses);

        return Ok(new { bosses });
    }

    /// <summary>
    /// Get a specific spell by ID
    /// </summary>
    [HttpGet("spells/{spellId}")]
    public async Task<IActionResult> GetSpell(int spellId)
    {
        _logger.LogInformation("Fetching spell: {SpellId}", spellId);

        var cached = await _cacheService.GetAsync<WowSpell>("world-data", $"spell_{spellId}");
        if (cached != null)
        {
            return Ok(cached);
        }

        var spell = _worldDataService.GetSpell(spellId);
        if (spell == null)
        {
            return NotFound(new { error = $"Spell not found: {spellId}" });
        }

        await _cacheService.SetAsync("world-data", $"spell_{spellId}", spell);

        return Ok(spell);
    }

    /// <summary>
    /// Get a specific trinket by ID
    /// </summary>
    [HttpGet("trinkets/{trinketId}")]
    public async Task<IActionResult> GetTrinket(int trinketId)
    {
        _logger.LogInformation("Fetching trinket: {TrinketId}", trinketId);

        var cached = await _cacheService.GetAsync<WowTrinket>("world-data", $"trinket_{trinketId}");
        if (cached != null)
        {
            return Ok(cached);
        }

        var trinket = _worldDataService.GetTrinket(trinketId);
        if (trinket == null)
        {
            return NotFound(new { error = $"Trinket not found: {trinketId}" });
        }

        await _cacheService.SetAsync("world-data", $"trinket_{trinketId}", trinket);

        return Ok(trinket);
    }

    /// <summary>
    /// Get all spells for a spec (abilities tracked in timeline)
    /// </summary>
    [HttpGet("specs/{specSlug}/spells")]
    public IActionResult GetSpecSpells(string specSlug)
    {
        var spec = _worldDataService.GetSpec(specSlug);
        if (spec == null)
        {
            return NotFound(new { error = $"Spec not found: {specSlug}" });
        }

        // Spell data per spec is populated from WCL data and stored externally
        return Ok(new { spells = Array.Empty<WowSpell>() });
    }

    /// <summary>
    /// Get all spells for a boss (boss abilities tracked in timeline)
    /// </summary>
    [HttpGet("bosses/{bossSlug}/spells")]
    public IActionResult GetBossSpells(string bossSlug)
    {
        var boss = _worldDataService.GetBoss(bossSlug);
        if (boss == null)
        {
            return NotFound(new { error = $"Boss not found: {bossSlug}" });
        }

        return Ok(new { spells = Array.Empty<WowSpell>() });
    }

    /// <summary>
    /// Get all WoW spells
    /// </summary>
    [HttpGet("spells")]
    public async Task<IActionResult> GetSpells()
    {
        _logger.LogInformation("Fetching all spells");

        var cached = await _cacheService.GetAsync<List<WowSpell>>("world-data", "spells");
        if (cached != null)
        {
            return Ok(new { spells = cached });
        }

        var spells = _worldDataService.Spells.OrderBy(s => s.Name).ToList();
        await _cacheService.SetAsync("world-data", "spells", spells);

        return Ok(new { spells });
    }

    /// <summary>
    /// Get all WoW trinkets
    /// </summary>
    [HttpGet("trinkets")]
    public async Task<IActionResult> GetTrinkets()
    {
        _logger.LogInformation("Fetching all trinkets");

        var cached = await _cacheService.GetAsync<List<WowTrinket>>("world-data", "trinkets");
        if (cached != null)
        {
            return Ok(new { trinkets = cached });
        }

        var trinkets = _worldDataService.Trinkets.OrderBy(t => t.Name).ToList();
        await _cacheService.SetAsync("world-data", "trinkets", trinkets);

        return Ok(new { trinkets });
    }

    /// <summary>
    /// Get all seasons
    /// </summary>
    [HttpGet("seasons")]
    public async Task<IActionResult> GetSeasons()
    {
        _logger.LogInformation("Fetching all seasons");

        var cached = await _cacheService.GetAsync<List<Season>>("world-data", "seasons");
        if (cached != null)
        {
            return Ok(new { seasons = cached });
        }

        var seasons = _worldDataService.Seasons.OrderBy(s => s.StartDate).ToList();
        await _cacheService.SetAsync("world-data", "seasons", seasons);

        return Ok(new { seasons });
    }

    /// <summary>
    /// Get a specific season by slug
    /// </summary>
    [HttpGet("seasons/{seasonSlug}")]
    public async Task<IActionResult> GetSeason(string seasonSlug)
    {
        _logger.LogInformation("Fetching season: {SeasonSlug}", seasonSlug);

        var cached = await _cacheService.GetAsync<Season>("world-data", $"season_{seasonSlug}");
        if (cached != null)
        {
            return Ok(cached);
        }

        var season = _worldDataService.GetSeason(seasonSlug);
        if (season == null)
        {
            return NotFound(new { error = $"Season not found: {seasonSlug}" });
        }

        await _cacheService.SetAsync("world-data", $"season_{seasonSlug}", season);

        return Ok(season);
    }

}
