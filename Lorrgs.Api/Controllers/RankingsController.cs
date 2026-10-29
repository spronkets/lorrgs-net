using Microsoft.AspNetCore.Mvc;
using Lorrgs.Api.Models;
using Lorrgs.Api.Services;
using Lorrgs.WarcraftLogs.Contracts;
using System.Text;
using System.Text.Json;
using System.Net;
using Microsoft.Extensions.Options;
using Lorrgs.WarcraftLogs;
using WclClient = Lorrgs.WarcraftLogs.WarcraftLogsClient;

namespace Lorrgs.Api.Controllers;

/// <summary>
/// Provides ranking data for specs and compositions.
/// Data is cached for 10 days by default.
/// This controller serves pre-computed rankings from the cache, avoiding real-time API calls.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RankingsController(
    ILogger<RankingsController> logger,
    WclClient warcraftLogsClient,
    IOptions<RaidCatalogOptions> raidCatalogOptions,
    IReportTimelineService timelineService) : ControllerBase
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
    private readonly WclClient _warcraftLogsClient = warcraftLogsClient;
    private readonly RaidCatalogOptions _raidCatalogOptions = raidCatalogOptions.Value;
    private readonly WorldDataService _worldDataService = WorldDataService.Instance;

    private static readonly string SpecRankingsQuery =
        GraphQlResourceLoader.Load("Rankings.SpecRankings.graphql");

    private static readonly string EncounterListQuery =
        GraphQlResourceLoader.Load("Rankings.EncounterList.graphql");

    private static readonly string ReportRankingsEnrichmentQueryTemplate =
        GraphQlResourceLoader.Load("Rankings.ReportRankingsEnrichment.graphql");

    private static readonly string CharacterFactionEnrichmentQueryTemplate =
        GraphQlResourceLoader.Load("Rankings.CharacterFactionEnrichment.graphql");

    /// <summary>
    /// Get full ranking data for a spec on a specific boss.
    /// Includes all top reports with fight details, player information and complete API-cached timelines.
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
        [FromQuery] bool includeOtherPlayers = true,
        CancellationToken cancellationToken = default)
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

        try
        {
            // Fetch from WarcraftLogs API
            var ranking = await FetchSpecRankingFromWCL(
                queryContext,
                difficultyValue.Value,
                difficultyLabel,
                normalizedMetric,
                edition,
                requestOptions,
                refresh,
                cancellationToken: cancellationToken);

            if (ranking == null)
            {
                return NotFound(new { error = "Ranking not found" });
            }

            await timelineService.PopulateFightPhasesAsync(ranking.Reports, edition ?? "retail", cancellationToken);
            var bossIcon = _worldDataService.GetBoss(queryContext.BossSlug)?.Icon;
            ranking.BossIconUrl = !string.IsNullOrWhiteSpace(bossIcon)
                ? $"https://wow.zamimg.com/images/wow/icons/medium/{bossIcon}"
                : null;
            foreach (var report in ranking.Reports.ToArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fight = report.Fights.FirstOrDefault();
                var player = report.Players.FirstOrDefault();
                if (fight is null || fight.FightId <= 0 || player is null ||
                    (player.PlayerId <= 0 && string.IsNullOrWhiteSpace(player.Name))) continue;
                try
                {
                    report.Timeline = await timelineService.TryLoadCachedAsync(report.ReportId,
                        fight.FightId, player.PlayerId > 0 ? player.PlayerId : null, player.Name,
                        edition ?? "retail", cancellationToken, player.SpecId);
                    report.BossTimeline = await timelineService.LoadBossAsync(report.ReportId, fight.FightId,
                        edition ?? "retail", true, cancellationToken);
                    if (report.BossTimeline is { IsComplete: true } reference)
                        fight.Phases = reference.Phases;
                }
                catch (Exception ex) when (ex is WarcraftLogsReportPrivateException or WarcraftLogsReportNotFoundException)
                {
                    _logger.LogWarning(ex, "Cached ranking timeline report unavailable report={ReportCode}", report.ReportId);
                    ranking.Reports.Remove(report);
                }
            }
            return Ok(ranking);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            _logger.LogWarning(ex,
                "WarcraftLogs rate limit hit while fetching spec ranking {SpecSlug}/{BossSlug} edition={Edition} difficulty={Difficulty} metric={Metric}",
                specSlug, bossSlug, edition, difficulty, metric);
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                error = "Warcraft Logs rate limit reached. Please retry shortly."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error fetching spec ranking {SpecSlug}/{BossSlug} edition={Edition} difficulty={Difficulty} metric={Metric}",
                specSlug, bossSlug, edition, difficulty, metric);
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
        [FromQuery] bool includeOtherPlayers = true,
        CancellationToken cancellationToken = default)
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

        try
        {
            var ranking = await FetchSpecRankingFromWCL(
                queryContext,
                difficultyValue.Value,
                difficultyLabel,
                normalizedMetric,
                edition,
                requestOptions,
                refresh,
                includeReportDetails: false,
                cancellationToken: cancellationToken);

            if (ranking == null)
            {
                return NotFound(new { error = "Ranking not found" });
            }

            // Return info-only payload without report details.
            ranking.Reports?.Clear();

            return Ok(ranking);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            _logger.LogWarning(ex,
                "WarcraftLogs rate limit hit while fetching spec ranking info {SpecSlug}/{BossSlug} edition={Edition} difficulty={Difficulty} metric={Metric}",
                specSlug, bossSlug, edition, difficulty, metric);
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                error = "Warcraft Logs rate limit reached. Please retry shortly."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error fetching spec ranking info {SpecSlug}/{BossSlug} edition={Edition} difficulty={Difficulty} metric={Metric}",
                specSlug, bossSlug, edition, difficulty, metric);
            return StatusCode(500, new { error = "Failed to fetch ranking info" });
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

        return Ok(new
        {
            message = dirty
                ? "Ranking refresh acknowledged. API response caching is disabled; use refresh=true to bypass WarcraftLogs response cache."
                : "Ranking marked clean",
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
        RankingRequestOptions requestOptions,
        bool bypassWclCache,
        bool includeReportDetails = true,
        CancellationToken cancellationToken = default)
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
                Reports = []
            };

            // Faction enrichment requires realm/region data from character rankings.
            // Always request combatant info so we can reliably resolve player faction.
            var activeOptions = requestOptions with { IncludeCombatantInfo = true };
            var activeDifficulty = (int?)difficultyValue;

            var queryResult = await QueryCharacterRankingsAsync(
                queryContext,
                metric,
                activeDifficulty,
                activeOptions,
                edition,
                bypassWclCache,
                cancellationToken);

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
                    edition,
                    bypassWclCache,
                    cancellationToken);

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
                    edition,
                    bypassWclCache,
                    cancellationToken);

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
                    edition,
                    bypassWclCache,
                    cancellationToken);

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

            if (!includeReportDetails)
            {
                return ranking;
            }

            foreach (var rankingElem in characterRankings.Rankings)
            {
                cancellationToken.ThrowIfCancellationRequested();
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
                    GuildName = string.IsNullOrWhiteSpace(rankingElem.Guild?.Name) ? null : rankingElem.Guild.Name,
                    FactionId = rankingElem.Guild?.Faction,
                    ServerRegion = ResolveServerRegion(rankingElem.ServerRegion, rankingElem.RegionName, rankingElem.RegionAlias)
                        ?? NormalizeServerRegion(activeOptions.ServerRegion),
                    ServerSlug = ResolveServerSlug(rankingElem.ServerSlug, rankingElem.ServerName, rankingElem.RealmName, rankingElem.ServerAlias)
                        ?? NormalizeServerSlug(activeOptions.ServerSlug),
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
                    Fights =
                    [
                        new RankingFight
                        {
                            FightId = reportData.FightId,
                            Name = queryContext.BossName,
                            Duration = rankingElem.Duration,
                            IsKill = rankingElem.Kill ?? true,
                            StartTime = UnixTimeStampToDateTime(reportData.StartTime),
                            Percentile = rankingElem.Percentile
                        }
                    ],
                    Players = BuildRankingPlayers(rankingElem, primaryPlayer, activeOptions)
                };

                ranking.Reports.Add(report);
            }

            await PopulatePlayerPercentilesAsync(ranking.Reports, queryContext, metric, edition, bypassWclCache, cancellationToken);
            await EnrichPlayerFactionsAsync(ranking.Reports, edition, bypassWclCache, cancellationToken);

            return ranking;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw;
        }
        catch (WarcraftLogsGraphQlException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error fetching spec ranking from WarcraftLogs API for {SpecSlug}/{BossSlug} edition={Edition} difficulty={Difficulty} metric={Metric}",
                queryContext.SpecSlug, queryContext.BossSlug, edition, difficultyValue, metric);
            throw;
        }
    }

    private async Task<(WclCharacterRankingsPayload? CharacterRankings, WclEncounterNode? Encounter)> QueryCharacterRankingsAsync(
        RankingQueryContext queryContext,
        string metric,
        int? difficulty,
        RankingRequestOptions requestOptions,
        string? edition,
        bool bypassWclCache,
        CancellationToken cancellationToken)
    {
        var encounterId = queryContext.EncounterId;
        if (encounterId <= 0)
        {
            var encounterList = await _warcraftLogsClient.QueryGraphQLAsync<WclSpecRankingsResponse>(
                EncounterListQuery, new { zoneId = queryContext.ZoneId }, edition, bypassWclCache, cancellationToken);
            var encounters = encounterList.WorldData?.Zone?.Encounters;
            if (encounters is null || !TryFindEncounter(encounters, queryContext, out var selectedEncounter))
            {
                _logger.LogWarning("Encounter {BossSlug} was not returned for zone {ZoneId}",
                    queryContext.BossSlug, queryContext.ZoneId);
                return (null, null);
            }
            encounterId = selectedEncounter.Id;
        }

        var variables = new
        {
            encounterId,
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
            edition,
            bypassWclCache,
            cancellationToken);

        var encounter = specRankingsResponse.WorldData?.Encounter;
        if (encounter is null)
        {
            _logger.LogWarning(
                "WarcraftLogs returned no encounter for {SpecSlug}/{BossSlug} encounter={EncounterId} edition={Edition}",
                queryContext.SpecSlug, queryContext.BossSlug, encounterId, edition);
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
        string? edition,
        bool bypassWclCache,
        CancellationToken cancellationToken)
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
            .GroupBy(report => report.ReportId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToList();

        if (unresolvedReports.Count == 0)
        {
            return;
        }

        const int chunkSize = 10;

        static string ReportSelection(string alias, IGrouping<string, RankingReport> group)
        {
            var fightIds = string.Join(", ", group.SelectMany(report => report.Fights)
                .Select(fight => fight.FightId).Distinct().Order());
            return $"    {alias}: report(code: \"{EscapeGraphQlString(group.Key)}\") {{ code title " +
                $"fights(fightIDs: [{fightIds}]) {{ id encounterID startTime endTime phaseTransitions {{ id startTime }} }} " +
                $"rankings(fightIDs: [{fightIds}], playerMetric: $playerMetric) }}";
        }

        for (var offset = 0; offset < unresolvedReports.Count; offset += chunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chunk = unresolvedReports.Skip(offset).Take(chunkSize).ToList();
            var aliasToReport = new Dictionary<string, IGrouping<string, RankingReport>>(StringComparer.OrdinalIgnoreCase);
            var reportsBlock = new StringBuilder();

            for (var i = 0; i < chunk.Count; i++)
            {
                var report = chunk[i];
                var alias = $"r{i}";
                aliasToReport[alias] = report;
                reportsBlock.AppendLine(ReportSelection(alias, report));
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
                    edition,
                    bypassWclCache,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw;
            }
            catch (WarcraftLogsGraphQlException ex) when (ex.IsReportAccessFailure)
            {
                var aliasesKnown = ex.Errors.All(error =>
                    error.ReportAlias is { } alias && aliasToReport.ContainsKey(alias));
                if (aliasesKnown && ex.PartialData is { ValueKind: JsonValueKind.Object } partialData &&
                    partialData.TryGetProperty("reportData", out var partialReports) &&
                    partialReports.ValueKind == JsonValueKind.Object)
                {
                    foreach (var alias in ex.InaccessibleReportAliases)
                    {
                        _logger.LogWarning("Ranking enrichment report unavailable report={ReportCode}", aliasToReport[alias].Key);
                        foreach (var report in aliasToReport[alias]) reports.Remove(report);
                    }
                    response = partialData;
                }
                else
                {
                    // Without usable paths or partial data, isolate only this failed batch.
                    var recoveredReports = new Dictionary<string, JsonElement>();
                    foreach (var entry in aliasToReport)
                    {
                        var singleQuery = ReportRankingsEnrichmentQueryTemplate.Replace(
                            "__REPORTS_BLOCK__", ReportSelection(entry.Key, entry.Value));
                        try
                        {
                            var singleResponse = await _warcraftLogsClient.QueryGraphQLAsync(singleQuery,
                                new { playerMetric = metricForReportRanking }, edition, bypassWclCache, cancellationToken);
                            if (singleResponse.ValueKind != JsonValueKind.Object ||
                                !singleResponse.TryGetProperty("reportData", out var singleReportData) ||
                                singleReportData.ValueKind != JsonValueKind.Object)
                                throw new InvalidOperationException("Warcraft Logs returned no report enrichment data.");
                            recoveredReports[entry.Key] = singleReportData.GetProperty(entry.Key).Clone();
                        }
                        catch (WarcraftLogsGraphQlException singleError) when (
                            singleError.IsReportAccessFailure ||
                            singleError is WarcraftLogsReportPrivateException or WarcraftLogsReportNotFoundException)
                        {
                            _logger.LogWarning(singleError, "Ranking enrichment report unavailable report={ReportCode}", entry.Value.Key);
                            foreach (var report in entry.Value) reports.Remove(report);
                        }
                    }
                    response = JsonSerializer.SerializeToElement(new { reportData = recoveredReports });
                }
            }
            catch (WarcraftLogsGraphQlException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to enrich report percentiles for {SpecSlug}/{BossSlug} edition={Edition} metric={Metric} batchOffset={BatchOffset} batchCount={BatchCount}",
                    queryContext.SpecSlug, queryContext.BossSlug, edition, metric, offset, chunk.Count);
                throw;
            }

            if (response.ValueKind != JsonValueKind.Object ||
                !response.TryGetProperty("reportData", out var reportData) || reportData.ValueKind != JsonValueKind.Object)
            {
                _logger.LogWarning(
                    "WarcraftLogs percentile enrichment returned no reportData for {SpecSlug}/{BossSlug} edition={Edition} batchOffset={BatchOffset} batchCount={BatchCount}",
                    queryContext.SpecSlug, queryContext.BossSlug, edition, offset, chunk.Count);
                continue;
            }

            foreach (var entry in aliasToReport)
            {
                if (!reportData.TryGetProperty(entry.Key, out var reportElement) || reportElement.ValueKind != JsonValueKind.Object)
                {
                    foreach (var report in entry.Value) reports.Remove(report);
                    continue;
                }

                foreach (var report in entry.Value)
                {
                    if (reportElement.TryGetProperty("fights", out var fightMetadata) && fightMetadata.ValueKind == JsonValueKind.Array)
                        ReportTimelineService.ApplyFightMetadata(report, fightMetadata);

                    if (!reportElement.TryGetProperty("rankings", out var rankingsElement))
                    {
                        continue;
                    }

                    var enrichedTitle = TryGetString(reportElement, "title");
                    if (!string.IsNullOrWhiteSpace(enrichedTitle))
                    {
                        report.Title = enrichedTitle;
                    }

                    var percentile = TryResolvePlayerRankPercent(rankingsElement, report, queryContext);
                    if (!percentile.HasValue)
                    {
                        continue;
                    }

                    report.Percentile = percentile;
                    foreach (var fight in report.Fights)
                    {
                        fight.Percentile = percentile;
                    }
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

    private List<RankingPlayer> BuildRankingPlayers(
        WclCharacterRankingEntry rankingElem,
        RankingPlayer primaryPlayer,
        RankingRequestOptions requestOptions)
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
                GuildName = string.IsNullOrWhiteSpace(character.GuildName) ? null : character.GuildName,
                ServerRegion = ResolveServerRegion(character.ServerRegion, character.RegionName, character.RegionAlias)
                    ?? primaryPlayer.ServerRegion
                    ?? NormalizeServerRegion(requestOptions.ServerRegion),
                ServerSlug = ResolveServerSlug(character.ServerSlug, character.ServerName, character.RealmName, character.ServerAlias)
                    ?? primaryPlayer.ServerSlug
                    ?? NormalizeServerSlug(requestOptions.ServerSlug),
                ClassId = mappedClass?.Id ?? 0,
                SpecId = mappedSpec?.Id ?? 0,
                SpecSlug = mappedSpec?.FullNameSlug ?? "unknown",
                Performance = 0
            });
        }

        return players;
    }

    private async Task EnrichPlayerFactionsAsync(List<RankingReport> reports, string? edition, bool bypassWclCache,
        CancellationToken cancellationToken)
    {
        if (reports.Count == 0)
        {
            return;
        }

        var players = reports
            .Where(static report => report.Players != null)
            .SelectMany(static report => report.Players)
            .Where(static player => !string.IsNullOrWhiteSpace(player.Name))
            .ToList();

        if (players.Count == 0)
        {
            return;
        }

        var lookupIndex = new Dictionary<string, List<RankingPlayer>>(StringComparer.OrdinalIgnoreCase);
        var lookupRequestsByKey = new Dictionary<string, CharacterLookupRequest>(StringComparer.OrdinalIgnoreCase);

        foreach (var player in players)
        {
            if (player.FactionId.HasValue && !string.IsNullOrWhiteSpace(player.GuildName))
            {
                continue;
            }

            var name = player.Name.Trim();
            var serverSlug = NormalizeServerSlug(player.ServerSlug);
            var serverRegion = NormalizeServerRegion(player.ServerRegion);
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(serverSlug) || string.IsNullOrWhiteSpace(serverRegion))
            {
                continue;
            }

            var key = BuildCharacterLookupKey(name, serverSlug, serverRegion);
            if (!lookupIndex.TryGetValue(key, out var existing))
            {
                existing = [];
                lookupIndex[key] = existing;
            }

            if (!lookupRequestsByKey.ContainsKey(key))
            {
                lookupRequestsByKey[key] = new CharacterLookupRequest(name, serverSlug, serverRegion, key);
            }

            existing.Add(player);
            player.ServerSlug = serverSlug;
            player.ServerRegion = serverRegion;
        }

        if (lookupIndex.Count == 0)
        {
            return;
        }

        var lookupRequests = lookupRequestsByKey.Values.OrderBy(request => request.Key, StringComparer.Ordinal).ToList();

        if (lookupRequests.Count == 0)
        {
            return;
        }

        const int chunkSize = 20;

        for (var offset = 0; offset < lookupRequests.Count; offset += chunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chunk = lookupRequests.Skip(offset).Take(chunkSize).ToList();
            var aliasToKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var charactersBlock = new StringBuilder();

            for (var i = 0; i < chunk.Count; i++)
            {
                var item = chunk[i];
                var alias = $"c{i}";
                aliasToKey[alias] = item.Key;
                charactersBlock.AppendLine(
                    $"    {alias}: character(name: \"{EscapeGraphQlString(item.Name)}\", serverSlug: \"{EscapeGraphQlString(item.ServerSlug)}\", serverRegion: \"{EscapeGraphQlString(item.ServerRegion)}\") {{ name faction {{ id }} guilds {{ name faction {{ id }} }} }}");
            }

            var queryText = CharacterFactionEnrichmentQueryTemplate.Replace(
                "__CHARACTERS_BLOCK__",
                charactersBlock.ToString().TrimEnd());

            JsonElement response;
            try
            {
                response = await _warcraftLogsClient.QueryGraphQLAsync(queryText, null, edition, bypassWclCache, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (WarcraftLogsGraphQlException)
            {
                throw;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to enrich ranking factions from WarcraftLogs characterData edition={Edition} batchOffset={BatchOffset} batchCount={BatchCount}",
                    edition, offset, chunk.Count);
                throw;
            }

            if (!response.TryGetProperty("characterData", out var characterData) || characterData.ValueKind != JsonValueKind.Object)
            {
                _logger.LogWarning(
                    "WarcraftLogs faction enrichment returned no characterData edition={Edition} batchOffset={BatchOffset} batchCount={BatchCount}",
                    edition, offset, chunk.Count);
                continue;
            }

            foreach (var alias in aliasToKey.Keys)
            {
                if (!characterData.TryGetProperty(alias, out var characterElement) || characterElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var playerFactionId = ReadFactionId(characterElement);
                int? guildFactionId = null;
                string? guildName = null;

                if (characterElement.TryGetProperty("guilds", out var guilds) &&
                    guilds.ValueKind == JsonValueKind.Array && guilds.GetArrayLength() == 1 &&
                    guilds[0].ValueKind == JsonValueKind.Object)
                {
                    var guildElement = guilds[0];
                    guildFactionId = ReadFactionId(guildElement);
                    guildName = TryGetString(guildElement, "name");
                }

                var key = aliasToKey[alias];
                if (!lookupIndex.TryGetValue(key, out var mappedPlayers))
                {
                    continue;
                }

                foreach (var player in mappedPlayers)
                {
                    if (!player.FactionId.HasValue)
                    {
                        player.FactionId = playerFactionId ?? guildFactionId;
                    }

                    if (string.IsNullOrWhiteSpace(player.GuildName) && !string.IsNullOrWhiteSpace(guildName))
                    {
                        player.GuildName = guildName;
                    }
                }
            }
        }
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

    private static string? NormalizeServerRegion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (string.Equals(normalized, "NA", StringComparison.OrdinalIgnoreCase))
        {
            return "US";
        }

        return normalized.ToUpperInvariant();
    }

    private static string? NormalizeServerSlug(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant();
    }

    private static string? ResolveServerRegion(string? serverRegion, string? regionName, string? regionAlias = null)
    {
        return NormalizeServerRegion(serverRegion)
            ?? NormalizeServerRegion(regionName)
            ?? NormalizeServerRegion(regionAlias);
    }

    private static string? ResolveServerSlug(string? serverSlug, string? serverName, string? realmName, string? serverAlias = null)
    {
        var direct = NormalizeServerSlug(serverSlug);
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return direct;
        }

        return SlugifyServerName(serverName)
            ?? SlugifyServerName(realmName)
            ?? SlugifyServerName(serverAlias);
    }

    private static string? SlugifyServerName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                continue;
            }

            builder.Append('-');
        }

        var slug = builder.ToString();
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        slug = slug.Trim('-');
        return slug.Length == 0 ? null : slug;
    }

    private static string BuildCharacterLookupKey(string name, string serverSlug, string serverRegion)
    {
        return $"{name.Trim().ToLowerInvariant()}|{serverSlug.Trim().ToLowerInvariant()}|{serverRegion.Trim().ToUpperInvariant()}";
    }

    private static string EscapeGraphQlString(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static int? ReadFactionId(JsonElement element)
    {
        if (!element.TryGetProperty("faction", out var faction))
        {
            return null;
        }

        if (faction.ValueKind == JsonValueKind.Object)
        {
            if (!faction.TryGetProperty("id", out faction))
            {
                return null;
            }
        }

        return int.TryParse(faction.ToString(), out var parsed) ? parsed : null;
    }

    private sealed record CharacterLookupRequest(
        string Name,
        string ServerSlug,
        string ServerRegion,
        string Key);

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
        var resolvedEncounterId = encounterId ?? (boss?.RaidId == zoneId ? boss?.Id : null);
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

    private static bool TryFindEncounter(IReadOnlyList<WclEncounterNode> encounters, RankingQueryContext queryContext, out WclEncounterNode encounter)
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
