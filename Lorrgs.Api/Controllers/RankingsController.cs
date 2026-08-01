using Microsoft.AspNetCore.Mvc;
using Lorrgs.Api.Models;
using Lorrgs.Api.Services;
using System.Text.Json;

namespace Lorrgs.Api.Controllers;

/// <summary>
/// Provides ranking data for specs and compositions.
/// Data is cached for 5 minutes. Background tasks (Phase 4) will update rankings from WarcraftLogs.
/// This controller serves pre-computed rankings from the cache, avoiding real-time API calls.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RankingsController(
    ILogger<RankingsController> logger,
    CacheService cacheService,
    WarcraftLogsClient warcraftLogsClient) : ControllerBase
{
    private static readonly HashSet<string> AllowedCharacterRankingMetrics = new(StringComparer.Ordinal)
    {
        "bosscdps",
        "bossdps",
        "bossndps",
        "bossrdps",
        "default",
        "dps",
        "hps",
        "krsi",
        "playerscore",
        "playerspeed",
        "cdps",
        "ndps",
        "rdps",
        "tankhps",
        "wdps",
        "healercombineddps",
        "healercombinedbossdps",
        "healercombinedcdps",
        "healercombinedbosscdps",
        "healercombinedndps",
        "healercombinedbossndps",
        "healercombinedrdps",
        "healercombinedbossrdps",
        "tankcombineddps",
        "tankcombinedbossdps",
        "tankcombinedcdps",
        "tankcombinedbosscdps",
        "tankcombinedndps",
        "tankcombinedbossndps",
        "tankcombinedrdps",
        "tankcombinedbossrdps",
    };

    private readonly ILogger<RankingsController> _logger = logger;
    private readonly CacheService _cacheService = cacheService;
    private readonly WarcraftLogsClient _warcraftLogsClient = warcraftLogsClient;
    private readonly WorldDataService _worldDataService = WorldDataService.Instance;

    // GraphQL query for spec rankings - follows WarcraftLogs API structure
        // Returns characterRankings for a specific zone encounter/spec/metric/difficulty
        private const string SpecRankingsQuery = """
                                query SpecRankings($zoneId: Int!, $className: String!, $specName: String!, $metric: CharacterRankingMetricType!, $difficulty: Int!) {
          worldData {
                        zone(id: $zoneId) {
                            encounters {
                                id
                                name
                                characterRankings(
                                    className: $className
                                    specName: $specName
                                    metric: $metric
                                    difficulty: $difficulty
                                    includeCombatantInfo: false
                                )
                            }
            }
          }
        }
    """;

    /// <summary>
    /// Get full ranking data for a spec on a specific boss.
    /// Includes all top reports with fight details and player information.
    /// </summary>
    [HttpGet("spec/{specSlug}/{bossSlug}")]
    public async Task<IActionResult> GetSpecRanking(
        string specSlug,
        string bossSlug,
        [FromQuery] string difficulty = "mythic",
        [FromQuery] string metric = "",
        [FromQuery] int? zoneId = null,
        [FromQuery] int? encounterId = null,
        [FromQuery] string? className = null,
        [FromQuery] string? specName = null,
        [FromQuery] int? difficultyId = null)
    {
        _logger.LogInformation("Fetching spec ranking: {SpecSlug}/{BossSlug} ({Difficulty}/{Metric})", 
            specSlug, bossSlug, difficulty, metric);

        if (!TryResolveSpecRankingQuery(specSlug, bossSlug, zoneId, encounterId, className, specName, out var queryContext, out var queryError))
        {
            return BadRequest(new { error = queryError });
        }

        var difficultyValue = ResolveDifficultyValue(difficulty, difficultyId);
        if (difficultyValue == null)
        {
            return BadRequest(new
            {
                error = "Invalid difficulty. Provide difficultyId or one of: lfr, normal, heroic, mythic."
            });
        }

        var difficultyLabel = BuildDifficultyLabel(difficulty, difficultyValue.Value);

        // Determine metric from spec role if not provided
        if (string.IsNullOrEmpty(metric))
        {
            var spec = _worldDataService.GetSpec(specSlug);
            metric = spec?.Role?.Metric ?? "dps";
        }

        var normalizedMetric = NormalizeMetric(metric);
        if (normalizedMetric == null)
        {
            return BadRequest(new
            {
                error = "Invalid metric for CharacterRankingMetricType"
            });
        }

        var cacheKey = BuildSpecRankingCacheKey(queryContext, difficultyValue.Value, normalizedMetric);

        // Try cache first (5-minute TTL for rankings, use short-lived cache)
        var cached = await _cacheService.GetAsync<SpecRankingData>("rankings", cacheKey);
        if (cached != null)
        {
            _logger.LogDebug("Returning cached spec ranking");
            return Ok(cached);
        }

        try
        {
            // Fetch from WarcraftLogs API
            var ranking = await FetchSpecRankingFromWCL(queryContext, difficultyValue.Value, difficultyLabel, normalizedMetric);
            
            if (ranking == null)
            {
                return NotFound(new { error = "Ranking not found" });
            }

            // Cache with 5-minute TTL (rankings change frequently)
            await CacheRankingWithShortTTL(cacheKey, ranking);

            return Ok(ranking);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching spec ranking");
            return StatusCode(500, new { error = "Failed to fetch ranking data" });
        }
    }

    /// <summary>
    /// Get ranking info without the full report details.
    /// Lighter response for quick info (percentile, updated time, etc).
    /// </summary>
    [HttpGet("spec/{specSlug}/{bossSlug}/info")]
    public async Task<IActionResult> GetSpecRankingInfo(
        string specSlug,
        string bossSlug,
        [FromQuery] string difficulty = "mythic",
        [FromQuery] string metric = "",
        [FromQuery] int? zoneId = null,
        [FromQuery] int? encounterId = null,
        [FromQuery] string? className = null,
        [FromQuery] string? specName = null,
        [FromQuery] int? difficultyId = null)
    {
        _logger.LogInformation("Fetching spec ranking info: {SpecSlug}/{BossSlug}", specSlug, bossSlug);

        if (!TryResolveSpecRankingQuery(specSlug, bossSlug, zoneId, encounterId, className, specName, out var queryContext, out var queryError))
        {
            return BadRequest(new { error = queryError });
        }

        var difficultyValue = ResolveDifficultyValue(difficulty, difficultyId);
        if (difficultyValue == null)
        {
            return BadRequest(new
            {
                error = "Invalid difficulty. Provide difficultyId or one of: lfr, normal, heroic, mythic."
            });
        }

        var difficultyLabel = BuildDifficultyLabel(difficulty, difficultyValue.Value);

        if (string.IsNullOrEmpty(metric))
        {
            var spec = _worldDataService.GetSpec(specSlug);
            metric = spec?.Role?.Metric ?? "dps";
        }

        var normalizedMetric = NormalizeMetric(metric);
        if (normalizedMetric == null)
        {
            return BadRequest(new
            {
                error = "Invalid metric for CharacterRankingMetricType"
            });
        }

        var cacheKey = $"{BuildSpecRankingCacheKey(queryContext, difficultyValue.Value, normalizedMetric)}_info";

        // Try cache
        var cached = await _cacheService.GetAsync<SpecRankingData>("rankings", cacheKey);
        if (cached != null)
        {
            _logger.LogDebug("Returning cached spec ranking info");
            // Clear reports from cached data before returning (info only, no reports)
            cached.Reports?.Clear();
            return Ok(cached);
        }

        try
        {
            var ranking = await FetchSpecRankingFromWCL(queryContext, difficultyValue.Value, difficultyLabel, normalizedMetric);
            
            if (ranking == null)
            {
                return NotFound(new { error = "Ranking not found" });
            }

            // Clear reports before caching (info only)
            ranking.Reports?.Clear();
            await CacheRankingWithShortTTL(cacheKey, ranking);

            return Ok(ranking);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching spec ranking info");
            return StatusCode(500, new { error = "Failed to fetch ranking info" });
        }
    }

    /// <summary>
    /// Get composition rankings for a specific boss.
    /// Shows optimal group setups with specs and roles.
    /// Supports filtering by role and spec.
    /// </summary>
    [HttpGet("comp/{bossSlug}")]
    public async Task<IActionResult> GetCompRanking(
        string bossSlug,
        [FromQuery] int limit = 20,
        [FromQuery(Name = "role")] string[]? roles = null,
        [FromQuery(Name = "spec")] string[]? specs = null)
    {
        _logger.LogInformation("Fetching comp ranking: {BossSlug} (limit={Limit})", bossSlug, limit);

        var filterKey = $"{string.Join(",", roles ?? Array.Empty<string>())}_{string.Join(",", specs ?? Array.Empty<string>())}";
        var cacheKey = $"{bossSlug}_{limit}_{filterKey}".TrimEnd('_');

        // Try cache
        var cached = await _cacheService.GetAsync<CompRankingData>("rankings", cacheKey);
        if (cached != null)
        {
            _logger.LogDebug("Returning cached comp ranking");
            return Ok(cached);
        }

        try
        {
            var ranking = await FetchCompRankingFromWCL(bossSlug, limit, roles, specs);
            
            if (ranking == null)
            {
                return NotFound(new { error = "Comp ranking not found" });
            }

            // Cache with 5-minute TTL
            await CacheRankingWithShortTTL(cacheKey, ranking);

            return Ok(ranking);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching comp ranking");
            return StatusCode(500, new { error = "Failed to fetch comp ranking" });
        }
    }

    /// <summary>
    /// Queue a background job to update spec rankings.
    /// Returns immediately with a task ID for tracking progress.
    /// </summary>
    [HttpPost("spec/queue")]
    public async Task<IActionResult> QueueSpecRankingUpdate(
        [FromQuery] string specSlug = "all",
        [FromQuery] string bossSlug = "all",
        [FromQuery] string difficulty = "all",
        [FromQuery] string metric = "all",
        [FromQuery] int limit = 50,
        [FromQuery] bool clear = false)
    {
        _logger.LogInformation("Queuing spec ranking update: {SpecSlug}/{BossSlug}", specSlug, bossSlug);

        // TODO: Implement task queue with Azure Service Bus
        // For now, return placeholder response
        return Ok(new
        {
            message = "Task queued for ranking update",
            taskId = Guid.NewGuid().ToString(),
            payload = new { specSlug, bossSlug, difficulty, metric, limit, clear }
        });
    }

    /// <summary>
    /// Queue a background job to update comp rankings.
    /// </summary>
    [HttpPost("comp/queue")]
    public async Task<IActionResult> QueueCompRankingUpdate(
        [FromQuery] string bossSlug = "all",
        [FromQuery] int limit = 50,
        [FromQuery] bool clear = false)
    {
        _logger.LogInformation("Queuing comp ranking update: {BossSlug}", bossSlug);

        // TODO: Implement task queue with Azure Service Bus
        return Ok(new
        {
            message = "Task queued for comp ranking update",
            taskId = Guid.NewGuid().ToString(),
            payload = new { bossSlug, limit, clear }
        });
    }

    /// <summary>
    /// Mark a specific spec ranking as dirty/outdated.
    /// This signals that the ranking should be refreshed from WarcraftLogs.
    /// </summary>
    [HttpPatch("spec/dirty")]
    public async Task<IActionResult> MarkSpecRankingDirty(
        [FromQuery] string specSlug,
        [FromQuery] string bossSlug,
        [FromQuery] string difficulty = "mythic",
        [FromQuery] string metric = "",
        [FromQuery] int? zoneId = null,
        [FromQuery] int? encounterId = null,
        [FromQuery] string? className = null,
        [FromQuery] string? specName = null,
        [FromQuery] int? difficultyId = null,
        [FromQuery] bool dirty = true)
    {
        _logger.LogInformation("Marking spec ranking dirty: {SpecSlug}/{BossSlug}", specSlug, bossSlug);

        if (!TryResolveSpecRankingQuery(specSlug, bossSlug, zoneId, encounterId, className, specName, out var queryContext, out var queryError))
        {
            return BadRequest(new { error = queryError });
        }

        var difficultyValue = ResolveDifficultyValue(difficulty, difficultyId);
        if (difficultyValue == null)
        {
            return BadRequest(new
            {
                error = "Invalid difficulty. Provide difficultyId or one of: lfr, normal, heroic, mythic."
            });
        }

        var difficultyLabel = BuildDifficultyLabel(difficulty, difficultyValue.Value);

        if (string.IsNullOrEmpty(metric))
        {
            var spec = _worldDataService.GetSpec(specSlug);
            metric = spec?.Role?.Metric ?? "dps";
        }

        var normalizedMetric = NormalizeMetric(metric);
        if (normalizedMetric == null)
        {
            return BadRequest(new
            {
                error = "Invalid metric for CharacterRankingMetricType"
            });
        }

        // Invalidate cache for this ranking
        var cacheKey = BuildSpecRankingCacheKey(queryContext, difficultyValue.Value, normalizedMetric);
        await _cacheService.InvalidateAsync("rankings", cacheKey);
        await _cacheService.InvalidateAsync("rankings", $"{cacheKey}_info");

        return Ok(new
        {
            message = dirty ? "Ranking marked dirty" : "Ranking marked clean",
            specSlug,
            bossSlug,
            difficulty = difficultyLabel,
            metric = normalizedMetric
        });
    }

    // ============================================================================
    // Private Helper Methods
    // ============================================================================

    /// <summary>
    /// Fetch spec ranking data from WarcraftLogs GraphQL API.
    /// Executes the characterRankings query and parses response into model objects.
    /// </summary>
    private async Task<SpecRankingData?> FetchSpecRankingFromWCL(
        RankingQueryContext queryContext, int difficultyValue, string difficultyLabel, string metric)
    {
        _logger.LogDebug(
            "Querying WarcraftLogs API for spec ranking: {SpecSlug}/{BossSlug} zone={ZoneId} encounter={EncounterId} class={ClassName} spec={SpecName}",
            queryContext.SpecSlug,
            queryContext.BossSlug,
            queryContext.ZoneId,
            queryContext.EncounterId,
            queryContext.ClassName,
            queryContext.SpecName);

        try
        {
            // Build GraphQL variables
            var variables = new
            {
                zoneId = queryContext.ZoneId,
                className = queryContext.ClassName,
                specName = queryContext.SpecName,
                metric,
                difficulty = difficultyValue
            };

            // Execute GraphQL query
            var response = await _warcraftLogsClient.QueryGraphQLAsync(
                SpecRankingsQuery,
                variables
            );

            // Parse response into SpecRankingData
            var ranking = new SpecRankingData
            {
                SpecSlug = queryContext.SpecSlug,
                BossSlug = queryContext.BossSlug,
                Difficulty = difficultyLabel,
                Metric = metric,
                Updated = DateTime.UtcNow,
                IsDirty = false,
                Reports = new List<RankingReport>()
            };

            // Extract rankings from response
            if (response.TryGetProperty("worldData", out var worldData) &&
                worldData.TryGetProperty("zone", out var zone) &&
                zone.TryGetProperty("encounters", out var encounters) &&
                encounters.ValueKind == JsonValueKind.Array)
            {
                var encounter = encounters.EnumerateArray().FirstOrDefault(encounterElem =>
                    TryGetInt32(encounterElem, "id") == queryContext.EncounterId);

                if (encounter.ValueKind == JsonValueKind.Undefined)
                {
                    _logger.LogWarning(
                        "Encounter {BossSlug} ({BossId}) was not returned for zone {ZoneId}",
                        queryContext.BossSlug,
                        queryContext.EncounterId,
                        queryContext.ZoneId);
                    return ranking;
                }

                if (encounter.TryGetProperty("characterRankings", out var characterRankings))
                {
                    var rankingsArray = characterRankings;
                    if (characterRankings.ValueKind == JsonValueKind.Object &&
                        characterRankings.TryGetProperty("rankings", out var nestedRankings))
                    {
                        rankingsArray = nestedRankings;
                    }

                    if (rankingsArray.ValueKind != JsonValueKind.Array)
                    {
                        _logger.LogWarning(
                            "characterRankings was not an array/object rankings payload for {SpecSlug}/{BossSlug}",
                            queryContext.SpecSlug,
                            queryContext.BossSlug);
                        return ranking;
                    }

                    var percentiles = new List<int>();

                    foreach (var rankingElem in rankingsArray.EnumerateArray())
                    {
                        // Skip hidden reports
                        if (rankingElem.TryGetProperty("hidden", out var hidden) && hidden.GetBoolean())
                            continue;

                        // Skip if no report data
                        if (!rankingElem.TryGetProperty("report", out var reportData))
                            continue;

                        var percentile = TryGetInt32(rankingElem, "percentile");
                        var duration = TryGetInt32(rankingElem, "duration");
                        var amount = TryGetDouble(rankingElem, "amount");
                        var rankingStart = TryGetInt64(rankingElem, "startTime");
                        var reportStart = TryGetInt64(reportData, "startTime");
                        var fightId = TryGetInt32(reportData, "fightID");
                        var isKill = TryGetBool(rankingElem, "kill") ?? true;

                        var report = new RankingReport
                        {
                            ReportId = TryGetString(reportData, "code") ?? "",
                            Title = TryGetString(reportData, "title") ?? "",
                            Percentile = percentile,
                            Duration = duration,
                            StartTime = UnixTimeStampToDateTime(rankingStart),
                            Fights = new List<RankingFight>
                            {
                                new RankingFight
                                {
                                    FightId = fightId,
                                    Name = queryContext.BossName,
                                    Duration = duration,
                                    IsKill = isKill,
                                    StartTime = UnixTimeStampToDateTime(reportStart),
                                    Percentile = percentile
                                }
                            },
                            Players = new List<RankingPlayer>
                            {
                                new RankingPlayer
                                {
                                    PlayerId = 0, // WarcraftLogs doesn't provide player ID in ranking response
                                    Name = TryGetString(rankingElem, "name") ?? "",
                                    ClassId = queryContext.ClassId,
                                    SpecId = queryContext.SpecId,
                                    SpecSlug = queryContext.SpecSlug,
                                    Performance = amount
                                }
                            }
                        };

                        if (!string.IsNullOrWhiteSpace(report.ReportId))
                        {
                            ranking.Reports.Add(report);
                            percentiles.Add(report.Percentile);
                        }
                    }

                    // Calculate average percentile
                    if (percentiles.Count > 0)
                    {
                        ranking.Percentile = (int)percentiles.Average();
                }
                }
            }

            return ranking;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching spec ranking from WarcraftLogs API");
            return null; // Return null instead of throwing to handle gracefully
        }
    }

    /// <summary>
    /// Fetch comp ranking data from WarcraftLogs GraphQL API.
    /// Note: Comp rankings are derived from multiple spec rankings, so this is a placeholder
    /// that aggregates data from cached spec rankings or returns empty for now.
    /// Full implementation will require building composition data from individual parses.
    /// </summary>
    private async Task<CompRankingData?> FetchCompRankingFromWCL(
        string bossSlug, int limit, string[]? roles, string[]? specs)
    {
        _logger.LogDebug("Querying WarcraftLogs API for comp ranking: {BossSlug}", bossSlug);

        try
        {
            // Composition rankings are computed from individual spec rankings
            // For now, return empty structure - real implementation will aggregate spec data
            var ranking = new CompRankingData
            {
                BossSlug = bossSlug,
                Updated = DateTime.UtcNow,
                Reports = new List<CompRankingReport>()
            };

            // TODO: Implement comp ranking aggregation from spec rankings
            // This would require:
            // 1. Query all available specs
            // 2. Fetch top reports for each spec
            // 3. Aggregate composition data (which specs were used together)
            // 4. Filter by role/spec if provided
            // 5. Return top {limit} compositions

            return ranking;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching comp ranking from WarcraftLogs API");
            return null;
        }
    }

    /// <summary>
    /// Cache a ranking with 5-minute TTL.
    /// Rankings change frequently, so we use a shorter cache time than world data.
    /// </summary>
    private async Task CacheRankingWithShortTTL<T>(string cacheKey, T data) where T : class
    {
        await _cacheService.SetAsync("rankings", cacheKey, data);
        _logger.LogDebug("Cached ranking: {CacheKey}", cacheKey);
    }

    /// <summary>
    /// Convert Unix timestamp to DateTime UTC.
    /// </summary>
    private static DateTime UnixTimeStampToDateTime(long unixTimeStamp)
    {
        if (unixTimeStamp <= 0)
        {
            return DateTime.UnixEpoch;
        }

        // Warcraft Logs commonly uses millisecond precision timestamps.
        return unixTimeStamp > 9_999_999_999
            ? DateTimeOffset.FromUnixTimeMilliseconds(unixTimeStamp).UtcDateTime
            : DateTimeOffset.FromUnixTimeSeconds(unixTimeStamp).UtcDateTime;
    }

    private static string? NormalizeDifficulty(string difficulty)
    {
        if (string.IsNullOrWhiteSpace(difficulty))
        {
            return "mythic";
        }

        var normalized = difficulty.Trim().ToLowerInvariant();
        return normalized switch
        {
            "normal" => "normal",
            "heroic" => "heroic",
            "mythic" => "mythic",
            _ => null
        };
    }

    private static int? ResolveDifficultyValue(string difficulty, int? difficultyId)
    {
        if (difficultyId.HasValue && difficultyId.Value > 0)
        {
            return difficultyId.Value;
        }

        if (string.IsNullOrWhiteSpace(difficulty))
        {
            return 5;
        }

        var normalized = difficulty.Trim().ToLowerInvariant();
        if (int.TryParse(normalized, out var parsedDifficulty) && parsedDifficulty > 0)
        {
            return parsedDifficulty;
        }

        return normalized switch
        {
            "lfr" => 2,
            "normal" => 3,
            "heroic" => 4,
            "mythic" => 5,
            _ => null
        };
    }

    private static string BuildDifficultyLabel(string difficulty, int difficultyValue)
    {
        if (string.IsNullOrWhiteSpace(difficulty))
        {
            return difficultyValue.ToString();
        }

        var normalized = difficulty.Trim().ToLowerInvariant();
        return normalized switch
        {
            "lfr" => "lfr",
            "normal" => "normal",
            "heroic" => "heroic",
            "mythic" => "mythic",
            _ => difficultyValue.ToString()
        };
    }

    private static string BuildSpecRankingCacheKey(RankingQueryContext queryContext, int difficultyValue, string metric)
    {
        return $"{queryContext.SpecSlug}_{queryContext.BossSlug}_{queryContext.ZoneId}_{queryContext.EncounterId}_{queryContext.ClassName}_{queryContext.SpecName}_{difficultyValue}_{metric}";
    }

    private bool TryResolveSpecRankingQuery(
        string specSlug,
        string bossSlug,
        int? zoneId,
        int? encounterId,
        string? className,
        string? specName,
        out RankingQueryContext queryContext,
        out string error)
    {
        var spec = _worldDataService.GetSpec(specSlug);
        var boss = _worldDataService.GetBoss(bossSlug);

        var resolvedZoneId = zoneId ?? boss?.RaidId;
        var resolvedEncounterId = encounterId ?? boss?.Id;
        var resolvedClassName = string.IsNullOrWhiteSpace(className)
            ? spec?.WowClass?.NameSlug
            : className.Trim().ToLowerInvariant();
        var resolvedSpecName = string.IsNullOrWhiteSpace(specName)
            ? spec?.NameSlug
            : specName.Trim().ToLowerInvariant();

        if (resolvedZoneId == null || resolvedZoneId.Value <= 0)
        {
            queryContext = default!;
            error = "Unable to resolve zoneId. Provide zoneId explicitly for Classic/Anniversary or unsupported raids.";
            return false;
        }

        if (resolvedEncounterId == null || resolvedEncounterId.Value <= 0)
        {
            queryContext = default!;
            error = "Unable to resolve encounterId. Provide encounterId explicitly for Classic/Anniversary or unsupported bosses.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(resolvedClassName))
        {
            queryContext = default!;
            error = "Unable to resolve className. Provide className explicitly for Classic/Anniversary specs.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(resolvedSpecName))
        {
            queryContext = default!;
            error = "Unable to resolve specName. Provide specName explicitly for Classic/Anniversary specs.";
            return false;
        }

        queryContext = new RankingQueryContext(
            resolvedZoneId.Value,
            resolvedEncounterId.Value,
            resolvedClassName,
            resolvedSpecName,
            spec?.FullNameSlug ?? specSlug,
            boss?.NameSlug ?? bossSlug,
            boss?.Name ?? bossSlug,
            spec?.WowClass?.Id ?? 0,
            spec?.Id ?? 0);

        error = string.Empty;
        return true;
    }

    private sealed record RankingQueryContext(
        int ZoneId,
        int EncounterId,
        string ClassName,
        string SpecName,
        string SpecSlug,
        string BossSlug,
        string BossName,
        int ClassId,
        int SpecId);

    private static string? NormalizeMetric(string metric)
    {
        var normalized = string.IsNullOrWhiteSpace(metric)
            ? "dps"
            : metric.Trim().ToLowerInvariant();

        return AllowedCharacterRankingMetrics.Contains(normalized)
            ? normalized
            : null;
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : property.ToString();
    }

    private static int TryGetInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return 0;
        }

        if (property.ValueKind == JsonValueKind.Number)
        {
            if (property.TryGetInt32(out var intValue))
            {
                return intValue;
            }

            if (property.TryGetDouble(out var doubleValue))
            {
                return (int)Math.Round(doubleValue);
            }
        }

        return 0;
    }

    private static long TryGetInt64(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return 0;
        }

        if (property.ValueKind == JsonValueKind.Number)
        {
            if (property.TryGetInt64(out var longValue))
            {
                return longValue;
            }

            if (property.TryGetDouble(out var doubleValue))
            {
                return (long)Math.Round(doubleValue);
            }
        }

        return 0;
    }

    private static double TryGetDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return 0;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var value))
        {
            return value;
        }

        return 0;
    }

    private static bool? TryGetBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

}
