using Microsoft.AspNetCore.Mvc;
using Lorrgs.Api.Models;
using Lorrgs.Api.Services;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Lorrgs.Api.Controllers;

/// <summary>
/// Provides static World of Warcraft game data: roles, classes, specs, bosses, etc.
/// Data is read directly from the server's hardcoded definitions.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class WorldDataController(
    ILogger<WorldDataController> logger) : ControllerBase, IActionFilter
{
    private readonly ILogger<WorldDataController> _logger = logger;
    private readonly WorldDataService _worldDataService = WorldDataService.Instance;

    [BindProperty(SupportsGet = true)]
    [FromQuery(Name = "edition")]
    public string? Edition { get; set; }

    [NonAction]
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var normalized = WorldDataEditions.NormalizeEdition(Edition);
        if (normalized == null)
        {
            _logger.LogWarning("World data requested with missing or unsupported edition: {Edition}", Edition);
            context.Result = BadRequest(new { error = "A supported edition query parameter is required." });
            return;
        }

        Edition = normalized;
    }

    [NonAction]
    public void OnActionExecuted(ActionExecutedContext context) { }

    /// <summary>
    /// Get all WoW roles (Tank, Healer, Melee DPS, Ranged DPS)
    /// </summary>
    [HttpGet("roles")]
    public IActionResult GetRoles()
    {
        _logger.LogInformation("Fetching all roles");

        var roles = _worldDataService.Roles.OrderBy(r => r.Id).ToList();

        return Ok(new { roles });
    }

    /// <summary>
    /// Get all WoW classes (Warrior, Mage, etc.)
    /// </summary>
    [HttpGet("classes")]
    public IActionResult GetClasses()
    {
        _logger.LogInformation("Fetching all classes");

        // Return as dictionary keyed by name slug
        var classes = _worldDataService.Classes
            .OrderBy(c => c.Name)
            .ToDictionary(c => c.NameSlug);

        return Ok(classes);
    }

    /// <summary>
    /// Get all WoW specs, optionally filtered by role or class
    /// </summary>
    [HttpGet("specs")]
    public IActionResult GetSpecs([FromQuery] int? roleId = null, [FromQuery] int? classId = null)
    {
        _logger.LogInformation("Fetching specs: roleId={RoleId}, classId={ClassId}", roleId, classId);

        var specs = _worldDataService.Specs.AsEnumerable();

        if (roleId.HasValue)
            specs = specs.Where(s => s.RoleId == roleId);

        if (classId.HasValue)
            specs = specs.Where(s => s.ClassId == classId);

        var result = specs.OrderBy(s => s.FullName).ToList();

        return Ok(new { specs = result });
    }

    /// <summary>
    /// Get a specific spec by its slug (e.g., "frost-mage")
    /// </summary>
    [HttpGet("specs/{specSlug}")]
    public IActionResult GetSpec(string specSlug)
    {
        _logger.LogInformation("Fetching spec: {SpecSlug}", specSlug);

        var spec = _worldDataService.GetSpec(specSlug);
        if (spec == null)
        {
            return NotFound(new { error = $"Spec not found: {specSlug}" });
        }

        return Ok(spec);
    }

    /// <summary>
    /// Get all specs for a specific class
    /// </summary>
    [HttpGet("classes/{classSlug}/specs")]
    public IActionResult GetClassSpecs(string classSlug)
    {
        _logger.LogInformation("Fetching specs for class: {ClassSlug}", classSlug);

        var wowClass = _worldDataService.Classes.FirstOrDefault(c => c.NameSlug == classSlug);
        if (wowClass == null)
        {
            return NotFound(new { error = $"Class not found: {classSlug}" });
        }

        var specs = _worldDataService.GetSpecsByClass(wowClass.Id)
            .OrderBy(s => s.Name)
            .ToList();

        return Ok(new { specs });
    }

    /// <summary>
    /// Get all specs for a specific role
    /// </summary>
    [HttpGet("roles/{roleSlug}/specs")]
    public IActionResult GetRoleSpecs(string roleSlug)
    {
        _logger.LogInformation("Fetching specs for role: {RoleSlug}", roleSlug);

        var role = _worldDataService.Roles.FirstOrDefault(r => r.NameSlug == roleSlug);
        if (role == null)
        {
            return NotFound(new { error = $"Role not found: {roleSlug}" });
        }

        var specs = _worldDataService.GetSpecsByRole(role.Id)
            .OrderBy(s => s.FullName)
            .ToList();

        return Ok(new { specs });
    }

    /// <summary>
    /// Get all raid bosses
    /// </summary>
    [HttpGet("bosses")]
    public IActionResult GetBosses()
    {
        _logger.LogInformation("Fetching all bosses");

        var bosses = _worldDataService.Bosses.OrderBy(b => b.Name).ToList();

        return Ok(new { bosses });
    }

    /// <summary>
    /// Get a specific boss by slug
    /// </summary>
    [HttpGet("bosses/{bossSlug}")]
    public IActionResult GetBoss(string bossSlug)
    {
        _logger.LogInformation("Fetching boss: {BossSlug}", bossSlug);

        var boss = _worldDataService.Bosses.FirstOrDefault(b => b.NameSlug == bossSlug);
        if (boss == null)
        {
            return NotFound(new { error = $"Boss not found: {bossSlug}" });
        }

        return Ok(boss);
    }

    /// <summary>
    /// Get all raid zones
    /// </summary>
    [HttpGet("zones")]
    public IActionResult GetZones()
    {
        _logger.LogInformation("Fetching all zones");

        var zones = _worldDataService.Zones.OrderByDescending(z => z.Id).ToList();

        return Ok(new { zones });
    }

    /// <summary>
    /// Get a specific zone by slug
    /// </summary>
    [HttpGet("zones/{zoneSlug}")]
    public IActionResult GetZone(string zoneSlug)
    {
        _logger.LogInformation("Fetching zone: {ZoneSlug}", zoneSlug);

        var zone = _worldDataService.GetZone(zoneSlug);
        if (zone == null)
        {
            return NotFound(new { error = $"Zone not found: {zoneSlug}" });
        }

        return Ok(zone);
    }

    /// <summary>
    /// Get all bosses in a zone
    /// </summary>
    [HttpGet("zones/{zoneSlug}/bosses")]
    public IActionResult GetZoneBosses(string zoneSlug)
    {
        _logger.LogInformation("Fetching bosses for zone: {ZoneSlug}", zoneSlug);

        var zone = _worldDataService.GetZone(zoneSlug);
        if (zone == null)
        {
            return NotFound(new { error = $"Zone not found: {zoneSlug}" });
        }

        var bosses = _worldDataService.Bosses
            .Where(b => b.RaidId == zone.Id)
            .ToList();

        return Ok(new { bosses });
    }

    /// <summary>
    /// Get a specific spell by ID
    /// </summary>
    [HttpGet("spells/{spellId}")]
    public IActionResult GetSpell(int spellId)
    {
        _logger.LogInformation("Fetching spell: {SpellId}", spellId);

        var spell = _worldDataService.GetSpell(spellId);
        if (spell == null)
        {
            return NotFound(new { error = $"Spell not found: {spellId}" });
        }

        return Ok(spell);
    }

    /// <summary>
    /// Get a specific trinket by ID
    /// </summary>
    [HttpGet("trinkets/{trinketId}")]
    public IActionResult GetTrinket(int trinketId)
    {
        _logger.LogInformation("Fetching trinket: {TrinketId}", trinketId);

        var trinket = _worldDataService.GetTrinket(trinketId);
        if (trinket == null)
        {
            return NotFound(new { error = $"Trinket not found: {trinketId}" });
        }

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
    public IActionResult GetSpells()
    {
        _logger.LogInformation("Fetching all spells");

        var spells = _worldDataService.Spells.OrderBy(s => s.Name).ToList();

        return Ok(new { spells });
    }

    /// <summary>
    /// Get all WoW trinkets
    /// </summary>
    [HttpGet("trinkets")]
    public IActionResult GetTrinkets()
    {
        _logger.LogInformation("Fetching all trinkets");

        var trinkets = _worldDataService.Trinkets.OrderBy(t => t.Name).ToList();

        return Ok(new { trinkets });
    }

    /// <summary>
    /// Get all seasons
    /// </summary>
    [HttpGet("seasons")]
    public IActionResult GetSeasons()
    {
        _logger.LogInformation("Fetching all seasons");

        var seasons = _worldDataService.Seasons.OrderBy(s => s.StartDate).ToList();

        return Ok(new { seasons });
    }

    /// <summary>
    /// Get a specific season by slug
    /// </summary>
    [HttpGet("seasons/{seasonSlug}")]
    public IActionResult GetSeason(string seasonSlug)
    {
        _logger.LogInformation("Fetching season: {SeasonSlug}", seasonSlug);

        var season = _worldDataService.GetSeason(seasonSlug);
        if (season == null)
        {
            return NotFound(new { error = $"Season not found: {seasonSlug}" });
        }

        return Ok(season);
    }

}
