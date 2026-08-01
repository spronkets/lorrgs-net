using Microsoft.AspNetCore.Mvc;
using Lorrgs.Api.Models;
using Lorrgs.Api.Services;
using Lorrgs.WarcraftLogs;
using Lorrgs.WarcraftLogs.Contracts;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WclClient = Lorrgs.WarcraftLogs.WarcraftLogsClient;

namespace Lorrgs.Api.Controllers;

/// <summary>
/// Provides ranking data for specs and compositions.
/// Data is cached for 1 day by default.
/// This controller serves pre-computed rankings from the cache, avoiding real-time API calls.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RankingsController(
    ILogger<RankingsController> logger,
    CacheService cacheService,
    WclClient warcraftLogsClient,
    IOptions<RaidCatalogOptions> raidCatalogOptions) : ControllerBase
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
    private readonly WclClient _warcraftLogsClient = warcraftLogsClient;
    private readonly RaidCatalogOptions _raidCatalogOptions = raidCatalogOptions.Value;
    private readonly WorldDataService _worldDataService = WorldDataService.Instance;

    private static readonly string SpecRankingsQuery =
        GraphQlResourceLoader.Load("Rankings.SpecRankings.graphql");

    private static readonly string ReportRankingsEnrichmentQueryTemplate =
        GraphQlResourceLoader.Load("Rankings.ReportRankingsEnrichment.graphql");

    private static readonly string ReportTitlesQueryTemplate =
        GraphQlResourceLoader.Load("Rankings.ReportTitles.graphql");

    /// <summary>
    /// Get full ranking data for a spec on a specific boss.
    /// Includes all top reports with fight details and player information.
    /// </summary>
    [HttpGet("spec/{specSlug}/{bossSlug}")]
    public async Task<IActionResult> GetSpecRanking(
        string specSlug,
        string bossSlug,
        [FromQuery] string? edition = null,
        [FromQuery] bool refresh = false,
        [FromQuery] string difficulty = "mythic",
        [FromQuery] string metric = "",
        [FromQuery] int? zoneId = null,
        [FromQuery] int? encounterId = null,
        [FromQuery] string? className = null,
        [FromQuery] string? specName = null,
        [FromQuery] int? difficultyId = null,
        [FromQuery] int? page = null,
        [FromQuery] int? partition = null,
        [FromQuery] int? bracket = null,
        [FromQuery] int? size = null,
        [FromQuery] string? serverRegion = null,
        [FromQuery] string? serverSlug = null,
        [FromQuery] string? filter = null,
        [FromQuery] string? hardModeLevel = null,
        [FromQuery] string? externalBuffs = null,
        [FromQuery] bool includeCombatantInfo = false,
        [FromQuery] bool includeOtherPlayers = true)
    {
        _logger.LogInformation("Fetching spec ranking: {SpecSlug}/{BossSlug} ({Difficulty}/{Metric})",
            specSlug, bossSlug, difficulty, metric);

        if (!TryResolveSpecRankingQuery(specSlug, bossSlug, edition, zoneId, encounterId, className, specName, out var queryContext, out var queryError))
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

        if (string.IsNullOrWhiteSpace(metric))
        {
            return BadRequest(new
            {
                error = "metric is required. Frontend must provide an explicit metric value."
            });
        }

        var normalizedMetric = NormalizeMetric(metric);
        if (normalizedMetric == null)
        {
            return BadRequest(new
            {
                error = "Invalid metric for CharacterRankingMetricType"
            });
        }

        if (!TryBuildRankingRequestOptions(page, partition, bracket, size, serverRegion, serverSlug, filter, hardModeLevel, externalBuffs, includeCombatantInfo, includeOtherPlayers, out var requestOptions, out var optionsError))
        {
            return BadRequest(new { error = optionsError });
        }

        var cacheKey = BuildSpecRankingCacheKey(queryContext, difficultyValue.Value, normalizedMetric, edition, requestOptions);

        if (!refresh)
        {
            var cached = await _cacheService.GetAsync<SpecRankingData>("rankings", cacheKey);
            if (cached != null)
            {
                if (ShouldRefetchCachedRanking(cached))
                {
                    _logger.LogInformation(
                        "Cached spec ranking appears stale (missing guild names). Refetching: {SpecSlug}/{BossSlug}",
                        specSlug,
                        bossSlug);
                }
                else
                {
                    _logger.LogDebug("Returning cached spec ranking");
                    return Ok(cached);
                }
            }
        }

        try
        {
            // Fetch from WarcraftLogs API
            var ranking = await FetchSpecRankingFromWCL(
                queryContext,
                difficultyValue.Value,
                difficultyLabel,
                normalizedMetric,
                edition,
                requestOptions);

            if (ranking == null)
            {
                return NotFound(new { error = "Ranking not found" });
            }

            // Cache persists for 1 day by default.
            await CacheRanking(cacheKey, ranking);

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
        [FromQuery] string? edition = null,
        [FromQuery] bool refresh = false,
        [FromQuery] string difficulty = "mythic",
        [FromQuery] string metric = "",
        [FromQuery] int? zoneId = null,
        [FromQuery] int? encounterId = null,
        [FromQuery] string? className = null,
        [FromQuery] string? specName = null,
        [FromQuery] int? difficultyId = null,
        [FromQuery] int? page = null,
        [FromQuery] int? partition = null,
        [FromQuery] int? bracket = null,
        [FromQuery] int? size = null,
        [FromQuery] string? serverRegion = null,
        [FromQuery] string? serverSlug = null,
        [FromQuery] string? filter = null,
        [FromQuery] string? hardModeLevel = null,
        [FromQuery] string? externalBuffs = null,
        [FromQuery] bool includeCombatantInfo = false,
        [FromQuery] bool includeOtherPlayers = true)
    {
        _logger.LogInformation("Fetching spec ranking info: {SpecSlug}/{BossSlug}", specSlug, bossSlug);

        if (!TryResolveSpecRankingQuery(specSlug, bossSlug, edition, zoneId, encounterId, className, specName, out var queryContext, out var queryError))
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

        if (string.IsNullOrWhiteSpace(metric))
        {
            return BadRequest(new
            {
                error = "metric is required. Frontend must provide an explicit metric value."
            });
        }

        var normalizedMetric = NormalizeMetric(metric);
        if (normalizedMetric == null)
        {
            return BadRequest(new
            {
                error = "Invalid metric for CharacterRankingMetricType"
            });
        }

        if (!TryBuildRankingRequestOptions(page, partition, bracket, size, serverRegion, serverSlug, filter, hardModeLevel, externalBuffs, includeCombatantInfo, includeOtherPlayers, out var requestOptions, out var optionsError))
        {
            return BadRequest(new { error = optionsError });
        }

        var cacheKey = $"{BuildSpecRankingCacheKey(queryContext, difficultyValue.Value, normalizedMetric, edition, requestOptions)}_info";

        if (!refresh)
        {
            var cached = await _cacheService.GetAsync<SpecRankingData>("rankings", cacheKey);
            if (cached != null)
            {
                _logger.LogDebug("Returning cached spec ranking info");
                // Clear reports from cached data before returning (info only, no reports)
                cached.Reports?.Clear();
                return Ok(cached);
            }
        }

        try
        {
            var ranking = await FetchSpecRankingFromWCL(
                queryContext,
                difficultyValue.Value,
                difficultyLabel,
                normalizedMetric,
                edition,
                requestOptions);

            if (ranking == null)
            {
                return NotFound(new { error = "Ranking not found" });
            }

            // Clear reports before caching (info only)
            ranking.Reports?.Clear();
            await CacheRanking(cacheKey, ranking);

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
        [FromQuery] bool refresh = false,
        [FromQuery(Name = "role")] string[]? roles = null,
        [FromQuery(Name = "spec")] string[]? specs = null)
    {
        _logger.LogInformation("Fetching comp ranking: {BossSlug} (limit={Limit})", bossSlug, limit);

        var filterKey = $"{string.Join(",", roles ?? [])}_{string.Join(",", specs ?? [])}";
        var cacheKey = $"{bossSlug}_{limit}_{filterKey}".TrimEnd('_');

        if (!refresh)
        {
            var cached = await _cacheService.GetAsync<CompRankingData>("rankings", cacheKey);
            if (cached != null)
            {
                _logger.LogDebug("Returning cached comp ranking");
                return Ok(cached);
            }
        }

        try
        {
            var ranking = await FetchCompRankingFromWCL(bossSlug, limit, roles, specs);

            if (ranking == null)
            {
                return NotFound(new { error = "Comp ranking not found" });
            }

            // Cache persists for 1 day by default.
            await CacheRanking(cacheKey, ranking);

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
        [FromQuery] string? edition = null,
        [FromQuery] string difficulty = "mythic",
        [FromQuery] string metric = "",
        [FromQuery] int? zoneId = null,
        [FromQuery] int? encounterId = null,
        [FromQuery] string? className = null,
        [FromQuery] string? specName = null,
        [FromQuery] int? difficultyId = null,
        [FromQuery] int? page = null,
        [FromQuery] int? partition = null,
        [FromQuery] int? bracket = null,
        [FromQuery] int? size = null,
        [FromQuery] string? serverRegion = null,
        [FromQuery] string? serverSlug = null,
        [FromQuery] string? filter = null,
        [FromQuery] string? hardModeLevel = null,
        [FromQuery] string? externalBuffs = null,
        [FromQuery] bool includeCombatantInfo = false,
        [FromQuery] bool includeOtherPlayers = true,
        [FromQuery] bool dirty = true)
    {
        _logger.LogInformation("Marking spec ranking dirty: {SpecSlug}/{BossSlug}", specSlug, bossSlug);

        if (!TryResolveSpecRankingQuery(specSlug, bossSlug, edition, zoneId, encounterId, className, specName, out var queryContext, out var queryError))
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

        if (string.IsNullOrWhiteSpace(metric))
        {
            return BadRequest(new
            {
                error = "metric is required. Frontend must provide an explicit metric value."
            });
        }

        var normalizedMetric = NormalizeMetric(metric);
        if (normalizedMetric == null)
        {
            return BadRequest(new
            {
                error = "Invalid metric for CharacterRankingMetricType"
            });
        }

        if (!TryBuildRankingRequestOptions(page, partition, bracket, size, serverRegion, serverSlug, filter, hardModeLevel, externalBuffs, includeCombatantInfo, includeOtherPlayers, out var requestOptions, out var optionsError))
        {
            return BadRequest(new { error = optionsError });
        }

        // Invalidate cache for this ranking
        var cacheKey = BuildSpecRankingCacheKey(queryContext, difficultyValue.Value, normalizedMetric, edition, requestOptions);
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
    /// Executes the characterRankings query and maps response DTOs into domain models.
    /// </summary>
    private async Task<SpecRankingData?> FetchSpecRankingFromWCL(
        RankingQueryContext queryContext,
        int difficultyValue,
        string difficultyLabel,
        string metric,
        string? edition,
        RankingRequestOptions requestOptions)
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
            var ranking = new SpecRankingData
            {
                SpecSlug = queryContext.SpecSlug,
                BossSlug = queryContext.BossSlug,
                Difficulty = difficultyLabel,
                Metric = metric,
                Page = requestOptions.Page,
                Updated = DateTime.UtcNow,
                IsDirty = false,
                Reports = new List<RankingReport>()
            };

            var activeOptions = requestOptions;
            var activeDifficulty = (int?)difficultyValue;

            var queryResult = await QueryCharacterRankingsAsync(
                queryContext,
                metric,
                activeDifficulty,
                activeOptions,
                edition);

            if (queryResult.CharacterRankings == null)
            {
                return ranking;
            }

            if (IsInvalidDifficultyOrSizeError(queryResult.CharacterRankings.Error) && activeOptions.Size.HasValue)
            {
                var sizeFallbackOptions = activeOptions with { Size = null };
                _logger.LogInformation(
                    "WarcraftLogs rejected size={Size} for {SpecSlug}/{BossSlug}; retrying without size filter",
                    activeOptions.Size,
                    queryContext.SpecSlug,
                    queryContext.BossSlug);

                var retryWithoutSize = await QueryCharacterRankingsAsync(
                    queryContext,
                    metric,
                    activeDifficulty,
                    sizeFallbackOptions,
                    edition);

                if (retryWithoutSize.CharacterRankings != null)
                {
                    queryResult = retryWithoutSize;
                    activeOptions = sizeFallbackOptions;
                }
            }

            // Some WCL partitions silently return zero rows when `size` is provided.
            // Retry once without size to recover real data for affected encounters.
            if (activeOptions.Size.HasValue &&
                queryResult.CharacterRankings.Count == 0 &&
                queryResult.CharacterRankings.Rankings.Count == 0)
            {
                var sizeFallbackOptions = activeOptions with { Size = null };
                _logger.LogInformation(
                    "WarcraftLogs returned empty rankings with size={Size} for {SpecSlug}/{BossSlug}; retrying without size filter",
                    activeOptions.Size,
                    queryContext.SpecSlug,
                    queryContext.BossSlug);

                var retryWithoutSize = await QueryCharacterRankingsAsync(
                    queryContext,
                    metric,
                    activeDifficulty,
                    sizeFallbackOptions,
                    edition);

                if (retryWithoutSize.CharacterRankings != null)
                {
                    queryResult = retryWithoutSize;
                    activeOptions = sizeFallbackOptions;
                }
            }

            if (IsInvalidDifficultyOrSizeError(queryResult.CharacterRankings?.Error) && activeDifficulty.HasValue)
            {
                _logger.LogInformation(
                    "WarcraftLogs rejected difficulty={Difficulty} for {SpecSlug}/{BossSlug}; retrying without explicit difficulty",
                    activeDifficulty,
                    queryContext.SpecSlug,
                    queryContext.BossSlug);

                var retryWithoutDifficulty = await QueryCharacterRankingsAsync(
                    queryContext,
                    metric,
                    null,
                    activeOptions,
                    edition);

                if (retryWithoutDifficulty.CharacterRankings != null)
                {
                    queryResult = retryWithoutDifficulty;
                }
            }

            var characterRankings = queryResult.CharacterRankings;
            if (characterRankings == null)
            {
                return ranking;
            }

            if (!string.IsNullOrWhiteSpace(characterRankings.Error))
            {
                _logger.LogWarning(
                    "WarcraftLogs characterRankings returned error for {SpecSlug}/{BossSlug}: {Error}",
                    queryContext.SpecSlug,
                    queryContext.BossSlug,
                    characterRankings.Error);
                return ranking;
            }

            if (characterRankings.Page > 0)
            {
                ranking.Page = characterRankings.Page;
            }

            ranking.TotalCount = characterRankings.Count;
            ranking.HasMorePages = characterRankings.HasMorePages;

            foreach (var rankingElem in characterRankings.Rankings)
            {
                if (rankingElem.Hidden)
                {
                    continue;
                }

                var reportData = rankingElem.Report;
                if (reportData == null || string.IsNullOrWhiteSpace(reportData.Code))
                {
                    continue;
                }

                var primaryPlayer = new RankingPlayer
                {
                    PlayerId = 0,
                    Name = rankingElem.Name ?? string.Empty,
                    GuildName = string.IsNullOrWhiteSpace(rankingElem.GuildName) ? null : rankingElem.GuildName,
                    ClassId = queryContext.ClassId,
                    SpecId = queryContext.SpecId,
                    SpecSlug = queryContext.SpecSlug,
                    Performance = rankingElem.Amount
                };

                var report = new RankingReport
                {
                    ReportId = reportData.Code,
                    Title = string.IsNullOrWhiteSpace(reportData.Title)
                        ? $"Report {reportData.Code}"
                        : reportData.Title,
                    Percentile = rankingElem.Percentile,
                    Duration = rankingElem.Duration,
                    StartTime = UnixTimeStampToDateTime(rankingElem.StartTime),
                    Fights = new List<RankingFight>
                    {
                        new RankingFight
                        {
                            FightId = reportData.FightId,
                            Name = queryContext.BossName,
                            Duration = rankingElem.Duration,
                            IsKill = rankingElem.Kill ?? true,
                            StartTime = UnixTimeStampToDateTime(reportData.StartTime),
                            Percentile = rankingElem.Percentile
                        }
                    },
                    Players = BuildRankingPlayers(rankingElem, primaryPlayer)
                };

                ranking.Reports.Add(report);
            }

            await PopulatePlayerPercentilesAsync(ranking.Reports, queryContext, metric, edition);
            await PopulateReportTitlesAsync(ranking.Reports, edition);

            return ranking;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching spec ranking from WarcraftLogs API");
            return null;
        }
    }

    private async Task<(WclCharacterRankingsPayload? CharacterRankings, WclEncounterNode? Encounter)> QueryCharacterRankingsAsync(
        RankingQueryContext queryContext,
        string metric,
        int? difficulty,
        RankingRequestOptions requestOptions,
        string? edition)
    {
        var variables = new
        {
            zoneId = queryContext.ZoneId,
            className = queryContext.ClassName,
            specName = queryContext.SpecName,
            metric,
            difficulty,
            page = requestOptions.Page,
            partition = requestOptions.Partition,
            bracket = requestOptions.Bracket,
            size = requestOptions.Size,
            serverRegion = requestOptions.ServerRegion,
            serverSlug = requestOptions.ServerSlug,
            filter = requestOptions.Filter,
            hardModeLevel = requestOptions.HardModeLevel,
            externalBuffs = requestOptions.ExternalBuffs,
            includeCombatantInfo = requestOptions.IncludeCombatantInfo,
            includeOtherPlayers = requestOptions.IncludeOtherPlayers
        };

        var specRankingsResponse = await _warcraftLogsClient.QueryGraphQLAsync<WclSpecRankingsResponse>(
            SpecRankingsQuery,
            variables,
            edition);

        var encounters = specRankingsResponse.WorldData?.Zone?.Encounters;
        if (encounters == null)
        {
            return (null, null);
        }

        if (!TryFindEncounter(encounters, queryContext, out var encounter))
        {
            _logger.LogWarning(
                "Encounter {BossSlug} ({BossId}) was not returned for zone {ZoneId}",
                queryContext.BossSlug,
                queryContext.EncounterId,
                queryContext.ZoneId);
            return (null, null);
        }

        return (WclCharacterRankingsParser.Parse(encounter.CharacterRankings), encounter);
    }

    private static bool IsInvalidDifficultyOrSizeError(string? error)
    {
        return !string.IsNullOrWhiteSpace(error)
            && error.Contains("Invalid difficulty setting or size specified.", StringComparison.OrdinalIgnoreCase);
    }

    private async Task PopulatePlayerPercentilesAsync(
        List<RankingReport> reports,
        RankingQueryContext queryContext,
        string metric,
        string? edition)
    {
        if (reports.Count == 0)
        {
            return;
        }

        var metricForReportRanking = ResolveReportRankingMetric(metric);
        if (metricForReportRanking == null)
        {
            return;
        }

        var unresolvedReports = reports
            .Where(static report =>
                !string.IsNullOrWhiteSpace(report.ReportId) &&
                (!report.Percentile.HasValue || report.Fights.Any(fight => !fight.Percentile.HasValue)))
            .ToList();

        if (unresolvedReports.Count == 0)
        {
            return;
        }

        const int chunkSize = 10;

        for (var offset = 0; offset < unresolvedReports.Count; offset += chunkSize)
        {
            var chunk = unresolvedReports.Skip(offset).Take(chunkSize).ToList();
            var aliasToReport = new Dictionary<string, RankingReport>(StringComparer.OrdinalIgnoreCase);
            var reportsBlock = new StringBuilder();

            for (var i = 0; i < chunk.Count; i++)
            {
                var report = chunk[i];
                var alias = $"r{i}";
                aliasToReport[alias] = report;
                reportsBlock.AppendLine($"    {alias}: report(code: \"{report.ReportId}\") {{ code rankings(playerMetric: $playerMetric) }}");
            }

            var queryText = ReportRankingsEnrichmentQueryTemplate.Replace(
                "__REPORTS_BLOCK__",
                reportsBlock.ToString().TrimEnd());

            JsonElement response;
            try
            {
                response = await _warcraftLogsClient.QueryGraphQLAsync(
                    queryText,
                    new { playerMetric = metricForReportRanking },
                    edition);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enrich report percentiles for ranking response");
                continue;
            }

            if (!response.TryGetProperty("reportData", out var reportData) || reportData.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var entry in aliasToReport)
            {
                if (!reportData.TryGetProperty(entry.Key, out var reportElement) || reportElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!reportElement.TryGetProperty("rankings", out var rankingsElement))
                {
                    continue;
                }

                var percentile = TryResolvePlayerRankPercent(rankingsElement, entry.Value, queryContext);
                if (!percentile.HasValue)
                {
                    continue;
                }

                entry.Value.Percentile = percentile;
                foreach (var fight in entry.Value.Fights)
                {
                    fight.Percentile = percentile;
                }
            }
        }
    }

    private static double? TryResolvePlayerRankPercent(
        JsonElement rankingsElement,
        RankingReport report,
        RankingQueryContext queryContext)
    {
        if (!TryGetRankingsDataArray(rankingsElement, out var rankingsData))
        {
            return null;
        }

        var primaryPlayer = report.Players.FirstOrDefault();
        if (primaryPlayer == null || string.IsNullOrWhiteSpace(primaryPlayer.Name))
        {
            return null;
        }

        var targetFightId = report.Fights.FirstOrDefault()?.FightId ?? 0;

        foreach (var rankingDataElem in rankingsData.EnumerateArray())
        {
            if (rankingDataElem.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (targetFightId > 0 && TryGetInt32(rankingDataElem, "fightID") != targetFightId)
            {
                continue;
            }

            if (!rankingDataElem.TryGetProperty("roles", out var rolesElement) || rolesElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var rankPercent = TryFindBestCharacterRankPercent(rolesElement, primaryPlayer, queryContext);
            if (rankPercent.HasValue)
            {
                return rankPercent;
            }
        }

        return null;
    }

    private static double? TryFindBestCharacterRankPercent(
        JsonElement rolesElement,
        RankingPlayer primaryPlayer,
        RankingQueryContext queryContext)
    {
        var targetName = primaryPlayer.Name.Trim();
        var targetClass = NormalizeComparableToken(queryContext.ClassName);
        var targetSpec = NormalizeComparableToken(queryContext.SpecName);

        var bestMatchScore = int.MinValue;
        var bestAmountDelta = double.MaxValue;
        double? bestRankPercent = null;

        foreach (var roleProperty in rolesElement.EnumerateObject())
        {
            if (roleProperty.Value.ValueKind != JsonValueKind.Object ||
                !roleProperty.Value.TryGetProperty("characters", out var charactersElement) ||
                charactersElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var character in charactersElement.EnumerateArray())
            {
                if (character.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = TryGetString(character, "name");
                if (!string.Equals(name?.Trim(), targetName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var rankPercent = TryGetNullableDouble(character, "rankPercent");
                if (!rankPercent.HasValue)
                {
                    continue;
                }

                var className = TryGetString(character, "class") ?? string.Empty;
                var specName = TryGetString(character, "spec") ?? string.Empty;
                var amount = TryGetDouble(character, "amount");

                var matchScore = 0;
                if (NormalizeComparableToken(className) == targetClass)
                {
                    matchScore += 2;
                }

                if (NormalizeComparableToken(specName) == targetSpec)
                {
                    matchScore += 3;
                }

                var amountDelta = Math.Abs(amount - primaryPlayer.Performance);
                if (matchScore > bestMatchScore || (matchScore == bestMatchScore && amountDelta < bestAmountDelta))
                {
                    bestMatchScore = matchScore;
                    bestAmountDelta = amountDelta;
                    bestRankPercent = rankPercent;
                }
            }
        }

        return bestRankPercent;
    }

    private static bool TryGetRankingsDataArray(JsonElement rankingsElement, out JsonElement dataArray)
    {
        if (rankingsElement.ValueKind == JsonValueKind.Array)
        {
            dataArray = rankingsElement;
            return true;
        }

        if (rankingsElement.ValueKind == JsonValueKind.Object &&
            rankingsElement.TryGetProperty("data", out var nestedData) &&
            nestedData.ValueKind == JsonValueKind.Array)
        {
            dataArray = nestedData;
            return true;
        }

        dataArray = default;
        return false;
    }

    private static bool ShouldRefetchCachedRanking(SpecRankingData cached)
    {
        if (cached.Reports == null || cached.Reports.Count == 0)
        {
            return false;
        }

        var hasAnyPlayers = cached.Reports.Any(r => r.Players != null && r.Players.Count > 0);
        if (!hasAnyPlayers)
        {
            return false;
        }

        var hasAnyGuildNames = cached.Reports
            .Where(r => r.Players != null)
            .SelectMany(r => r.Players)
            .Any(p => !string.IsNullOrWhiteSpace(p.GuildName));

        return !hasAnyGuildNames;
    }

    private static string? ResolveReportRankingMetric(string metric)
    {
        var normalized = string.IsNullOrWhiteSpace(metric)
            ? "dps"
            : metric.Trim().ToLowerInvariant();

        return normalized switch
        {
            "dps" => "dps",
            "hps" => "hps",
            "rdps" => "rdps",
            "ndps" => "ndps",
            "wdps" => "wdps",
            "cdps" => "cdps",
            _ => null
        };
    }

    private List<RankingPlayer> BuildRankingPlayers(WclCharacterRankingEntry rankingElem, RankingPlayer primaryPlayer)
    {
        var players = new List<RankingPlayer> { primaryPlayer };

        if (rankingElem.AllCharacters == null || rankingElem.AllCharacters.Count == 0)
        {
            return players;
        }

        foreach (var character in rankingElem.AllCharacters)
        {
            var name = character.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var className = character.Class;
            var specName = character.Spec;

            if (string.Equals(name, primaryPlayer.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(className, _worldDataService.GetClass(primaryPlayer.ClassId)?.Name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var mappedClass = !string.IsNullOrWhiteSpace(className)
                ? _worldDataService.Classes.FirstOrDefault(c => string.Equals(c.Name, className, StringComparison.OrdinalIgnoreCase))
                : null;

            var mappedSpec = ResolveSpecByNames(mappedClass?.Id, specName);

            players.Add(new RankingPlayer
            {
                PlayerId = 0,
                Name = name,
                ClassId = mappedClass?.Id ?? 0,
                SpecId = mappedSpec?.Id ?? 0,
                SpecSlug = mappedSpec?.FullNameSlug ?? "unknown",
                Performance = 0
            });
        }

        return players;
    }

    private WowSpec? ResolveSpecByNames(int? classId, string? specName)
    {
        if (classId == null || string.IsNullOrWhiteSpace(specName))
        {
            return null;
        }

        var normalizedSpecName = NormalizeComparableToken(specName);
        return _worldDataService.Specs.FirstOrDefault(spec =>
            spec.ClassId == classId.Value &&
            (
                NormalizeComparableToken(spec.Name) == normalizedSpecName ||
                NormalizeComparableToken(spec.NameSlug) == normalizedSpecName
            ));
    }

    private static string NormalizeComparableToken(string value)
    {
        var chars = value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray();

        return new string(chars);
    }

    private async Task PopulateReportTitlesAsync(List<RankingReport> reports, string? edition)
    {
        if (reports.Count == 0)
        {
            return;
        }

        var unresolvedCodes = reports
            .Where(static r => !string.IsNullOrWhiteSpace(r.ReportId))
            .Select(static r => r.ReportId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (unresolvedCodes.Count == 0)
        {
            return;
        }

        var titleByCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        const int chunkSize = 20;

        for (var offset = 0; offset < unresolvedCodes.Count; offset += chunkSize)
        {
            var chunk = unresolvedCodes.Skip(offset).Take(chunkSize).ToList();
            var aliasToCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var reportsBlock = new StringBuilder();

            for (var i = 0; i < chunk.Count; i++)
            {
                var alias = $"r{i}";
                aliasToCode[alias] = chunk[i];
                reportsBlock.AppendLine($"    {alias}: report(code: \"{chunk[i]}\") {{ code title }}");
            }

            var queryText = ReportTitlesQueryTemplate.Replace(
                "__REPORTS_BLOCK__",
                reportsBlock.ToString().TrimEnd());

            JsonElement response;
            try
            {
                response = await _warcraftLogsClient.QueryGraphQLAsync(queryText, null, edition);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enrich report titles for ranking response");
                continue;
            }

            if (!response.TryGetProperty("reportData", out var reportData) || reportData.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var alias in aliasToCode.Keys)
            {
                if (!reportData.TryGetProperty(alias, out var reportElement) || reportElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var code = TryGetString(reportElement, "code");
                var title = TryGetString(reportElement, "title");
                if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(title))
                {
                    titleByCode[code] = title;
                }
            }
        }

        if (titleByCode.Count == 0)
        {
            return;
        }

        foreach (var report in reports)
        {
            if (!string.IsNullOrWhiteSpace(report.ReportId) && titleByCode.TryGetValue(report.ReportId, out var resolvedTitle))
            {
                report.Title = resolvedTitle;
            }
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
    /// Cache a ranking using the default cache TTL.
    /// </summary>
    private async Task CacheRanking<T>(string cacheKey, T data) where T : class
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

    private static string BuildSpecRankingCacheKey(
        RankingQueryContext queryContext,
        int difficultyValue,
        string metric,
        string? edition,
        RankingRequestOptions requestOptions)
    {
        var editionSegment = string.IsNullOrWhiteSpace(edition)
            ? "default"
            : string.Concat(edition.Where(char.IsLetterOrDigit)).ToLowerInvariant();

        var requestSegment = BuildRankingRequestSegment(requestOptions);

        return $"{editionSegment}_{queryContext.SpecSlug}_{queryContext.BossSlug}_{queryContext.ZoneId}_{queryContext.EncounterId}_{queryContext.ClassName}_{queryContext.SpecName}_{difficultyValue}_{metric}_{requestSegment}";
    }

    private static string BuildRankingRequestSegment(RankingRequestOptions options)
    {
        var filterSegment = string.IsNullOrWhiteSpace(options.Filter)
            ? "nofilter"
            : string.Concat(options.Filter.Where(char.IsLetterOrDigit)).ToLowerInvariant();

        var regionSegment = string.IsNullOrWhiteSpace(options.ServerRegion)
            ? "allregions"
            : options.ServerRegion.Trim().ToLowerInvariant();

        var serverSegment = string.IsNullOrWhiteSpace(options.ServerSlug)
            ? "allservers"
            : options.ServerSlug.Trim().ToLowerInvariant();

        var hardModeSegment = string.IsNullOrWhiteSpace(options.HardModeLevel)
            ? "nahm"
            : string.Concat(options.HardModeLevel.Where(char.IsLetterOrDigit)).ToLowerInvariant();

        var externalBuffsSegment = string.IsNullOrWhiteSpace(options.ExternalBuffs)
            ? "naeb"
            : string.Concat(options.ExternalBuffs.Where(char.IsLetterOrDigit)).ToLowerInvariant();

        return $"p{options.Page}_part{options.Partition?.ToString() ?? "na"}_br{options.Bracket?.ToString() ?? "na"}_sz{options.Size?.ToString() ?? "na"}_r{regionSegment}_s{serverSegment}_f{filterSegment}_hm{hardModeSegment}_eb{externalBuffsSegment}_oc{(options.IncludeOtherPlayers ? 1 : 0)}_cc{(options.IncludeCombatantInfo ? 1 : 0)}";
    }

    private static bool TryBuildRankingRequestOptions(
        int? page,
        int? partition,
        int? bracket,
        int? size,
        string? serverRegion,
        string? serverSlug,
        string? filter,
        string? hardModeLevel,
        string? externalBuffs,
        bool includeCombatantInfo,
        bool includeOtherPlayers,
        out RankingRequestOptions options,
        out string error)
    {
        if (page.HasValue && page.Value <= 0)
        {
            options = default;
            error = "page must be greater than 0.";
            return false;
        }

        if (partition.HasValue && partition.Value <= 0)
        {
            options = default;
            error = "partition must be greater than 0.";
            return false;
        }

        if (bracket.HasValue && bracket.Value <= 0)
        {
            options = default;
            error = "bracket must be greater than 0.";
            return false;
        }

        if (size.HasValue && size.Value <= 0)
        {
            options = default;
            error = "size must be greater than 0.";
            return false;
        }

        if (!TryNormalizeHardModeLevel(hardModeLevel, out var normalizedHardModeLevel, out var hardModeError))
        {
            options = default;
            error = hardModeError;
            return false;
        }

        if (!TryNormalizeExternalBuffs(externalBuffs, out var normalizedExternalBuffs, out var externalBuffsError))
        {
            options = default;
            error = externalBuffsError;
            return false;
        }

        options = new RankingRequestOptions(
            page ?? 1,
            partition,
            bracket,
            size,
            NormalizeOptionalValue(serverRegion),
            NormalizeOptionalValue(serverSlug),
            NormalizeOptionalValue(filter),
            normalizedHardModeLevel,
            normalizedExternalBuffs,
            includeOtherPlayers,
            includeCombatantInfo);

        error = string.Empty;
        return true;
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static bool TryNormalizeHardModeLevel(string? value, out string? normalizedValue, out string error)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            normalizedValue = null;
            error = string.Empty;
            return true;
        }

        var raw = value.Trim();
        var key = raw.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        normalizedValue = key switch
        {
            "any" => "Any",
            "-1" => "Any",
            "highest" => "Highest",
            "normalmode" => "NormalMode",
            "normal" => "NormalMode",
            "level0" => "Level0",
            "0" => "Level0",
            "level1" => "Level1",
            "1" => "Level1",
            "level2" => "Level2",
            "2" => "Level2",
            "level3" => "Level3",
            "3" => "Level3",
            "level4" => "Level4",
            "4" => "Level4",
            _ => null
        };

        if (normalizedValue != null)
        {
            error = string.Empty;
            return true;
        }

        error = "Invalid hardModeLevel. Supported values: Any, Highest, NormalMode, Level0, Level1, Level2, Level3, Level4.";
        return false;
    }

    private static bool TryNormalizeExternalBuffs(string? value, out string? normalizedValue, out string error)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            normalizedValue = null;
            error = string.Empty;
            return true;
        }

        var raw = value.Trim();
        var key = raw.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        normalizedValue = key switch
        {
            "any" => "Any",
            "require" => "Require",
            "with" => "Require",
            "include" => "Require",
            "withexternalbuffs" => "Require",
            "exclude" => "Exclude",
            "without" => "Exclude",
            "no" => "Exclude",
            "withoutexternalbuffs" => "Exclude",
            _ => null
        };

        if (normalizedValue != null)
        {
            error = string.Empty;
            return true;
        }

        error = "Invalid externalBuffs. Supported values: Any, Require, Exclude.";
        return false;
    }

    private bool TryResolveSpecRankingQuery(
        string specSlug,
        string bossSlug,
        string? edition,
        int? zoneId,
        int? encounterId,
        string? className,
        string? specName,
        out RankingQueryContext queryContext,
        out string error)
    {
        var spec = _worldDataService.GetSpec(specSlug);
        var boss = _worldDataService.GetBoss(bossSlug);
        var requestedEdition = NormalizeOptionalValue(edition);

        var resolvedZoneId = zoneId;
        var resolvedEncounterId = encounterId;
        var resolvedClassName = NormalizeOptionalValue(className);
        var resolvedSpecName = NormalizeOptionalValue(specName);
        var resolvedBossName = boss?.Name ?? bossSlug;
        var resolvedBossSlug = boss?.NameSlug ?? bossSlug;

        if (string.IsNullOrWhiteSpace(requestedEdition))
        {
            queryContext = default!;
            error = "edition is required. Frontend must provide an explicit edition slug.";
            return false;
        }

        if (resolvedZoneId == null || resolvedZoneId.Value <= 0)
        {
            queryContext = default!;
            error = "zoneId is required. Frontend must provide an explicit raid zone id.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(resolvedClassName))
        {
            queryContext = default!;
            error = "className is required. Frontend must provide an explicit class name.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(resolvedSpecName))
        {
            queryContext = default!;
            error = "specName is required. Frontend must provide an explicit spec name.";
            return false;
        }

        queryContext = new RankingQueryContext(
            resolvedZoneId.Value,
            resolvedEncounterId.GetValueOrDefault(),
            resolvedClassName,
            resolvedSpecName,
            spec?.FullNameSlug ?? specSlug,
            resolvedBossSlug,
            resolvedBossName,
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

    private readonly record struct RankingRequestOptions(
        int Page,
        int? Partition,
        int? Bracket,
        int? Size,
        string? ServerRegion,
        string? ServerSlug,
        string? Filter,
        string? HardModeLevel,
        string? ExternalBuffs,
        bool IncludeOtherPlayers,
        bool IncludeCombatantInfo);

    private bool TryFindEncounter(IReadOnlyList<WclEncounterNode> encounters, RankingQueryContext queryContext, out WclEncounterNode encounter)
    {
        if (queryContext.EncounterId > 0)
        {
            encounter = encounters.FirstOrDefault(encounterElem => encounterElem.Id == queryContext.EncounterId)!;
            if (encounter != null)
            {
                return true;
            }
        }

        var targetSlug = NormalizeBossSlugForLookup(queryContext.BossSlug);
        if (string.IsNullOrWhiteSpace(targetSlug))
        {
            encounter = null!;
            return false;
        }

        encounter = encounters.FirstOrDefault(encounterElem =>
            string.Equals(
                NormalizeBossSlugForLookup(SlugifyEncounterName(encounterElem.Name)),
                targetSlug,
                StringComparison.OrdinalIgnoreCase))!;

        return encounter != null;
    }

    private static string SlugifyEncounterName(string? value)
    {
        var raw = value ?? string.Empty;
        raw = raw.Replace("'", string.Empty).Replace("’", string.Empty);
        var chars = raw
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return slug.Trim('-');
    }

    private static string NormalizeBossSlugForLookup(string? value)
    {
        var slug = (value ?? string.Empty).Trim().ToLowerInvariant().Trim('-');
        if (slug.Length == 0)
        {
            return string.Empty;
        }

        var lastDashIndex = slug.LastIndexOf('-');
        if (lastDashIndex > 0 && int.TryParse(slug[(lastDashIndex + 1)..], out _))
        {
            return slug[..lastDashIndex];
        }

        return slug;
    }

    private static string? NormalizeMetric(string metric)
    {
        var normalized = metric.Trim().ToLowerInvariant();

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

    private static double? TryGetNullableDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var value))
        {
            return value;
        }

        return null;
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
