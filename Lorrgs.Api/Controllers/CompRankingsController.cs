using System.Net;
using Lorrgs.Api.Models;
using Lorrgs.Api.Services;
using Lorrgs.WarcraftLogs;
using Microsoft.AspNetCore.Mvc;

namespace Lorrgs.Api.Controllers;

[ApiController]
[Route("api/rankings/comp")]
public sealed class CompRankingsController(ICompRankingService service, ILogger<CompRankingsController> logger) : ControllerBase
{
    /// <summary>Return the complete execution-ranked summary page. Composition is uninspected until details are requested.</summary>
    [HttpGet("{bossSlug}")]
    [ProducesResponseType<CompRankingData>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetCompRanking(string bossSlug,
        [FromQuery] string edition = "", [FromQuery] int encounterId = 0,
        [FromQuery] int difficultyId = 5, [FromQuery] int page = 1,
        [FromQuery] bool refresh = false, [FromQuery(Name = "role")] string[]? roles = null,
        [FromQuery(Name = "spec")] string[]? specs = null,
        [FromQuery(Name = "killtime_min")] int minSeconds = 0,
        [FromQuery(Name = "killtime_max")] int maxSeconds = 0,
        CancellationToken cancellationToken = default)
    {
        var normalizedEdition = WorldDataEditions.NormalizeEdition(edition);
        if (normalizedEdition is null || encounterId <= 0 || difficultyId is < 1 or > 5 ||
            page is < 1 or > 100 || minSeconds is < 0 or > 86400 ||
            maxSeconds is < 0 or > 86400 || (maxSeconds > 0 && maxSeconds < minSeconds) ||
            (roles?.Length ?? 0) + (specs?.Length ?? 0) > 100)
            return Problem(statusCode: 400, title: "Invalid composition search",
                detail: "Provide a supported edition, encounterId, difficultyId (1–5), page (1–100), and an ordered nonnegative kill-time range in seconds.");
        CompRankingSearch search;
        try
        {
            var roleFilters = (roles ?? []).Select(CompCountFilter.Parse).ToArray();
            var specFilters = (specs ?? []).Select(CompCountFilter.Parse).ToArray();
            if (roleFilters.Any(filter => filter.Key is not ("tank" or "heal" or "mdps" or "rdps")))
                throw new ArgumentException("Roles must be tank, heal, mdps, or rdps.");
            search = new(bossSlug, normalizedEdition, encounterId, difficultyId, page,
                minSeconds, maxSeconds, roleFilters, specFilters, refresh);
        }
        catch (ArgumentException ex) { return Problem(statusCode: 400, title: "Invalid count filter", detail: ex.Message); }
        try
        {
            var result = await service.LoadAsync(search, cancellationToken);
            return result is null ? Problem(statusCode: 404, title: "Encounter not found") : Ok(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return Problem(statusCode: 429, title: "Warcraft Logs rate limit reached. Please retry shortly.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Composition search failed encounter={EncounterId} edition={Edition}", encounterId, normalizedEdition);
            return Problem(statusCode: 502, title: "Unable to load composition rankings from Warcraft Logs.");
        }

    }

    /// <summary>Inspect one kill's composition and timelines using shared metadata and cooldown events from every fight in its report.</summary>
    [HttpGet("reports/{code}/fights/{fightId:int}")]
    [ProducesResponseType<RankingReport>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetDetails(string code, int fightId, [FromQuery] string edition = "",
        [FromQuery] bool refresh = false, CancellationToken cancellationToken = default)
    {
        var normalized = WorldDataEditions.NormalizeEdition(edition);
        if (normalized is null || fightId <= 0 || code.Length > 32 || !code.All(char.IsAsciiLetterOrDigit))
            return Problem(statusCode: 400, title: "Provide a valid report, fight ID and supported edition.");
        try { return Ok(await service.LoadDetailsAsync(code, fightId, normalized, refresh, cancellationToken)); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (WarcraftLogsReportPrivateException) { return Problem(statusCode: 403, title: "This Warcraft Logs report is private or archived."); }
        catch (WarcraftLogsReportNotFoundException) { return Problem(statusCode: 404, title: "This Warcraft Logs report or fight is unavailable."); }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        { return Problem(statusCode: 429, title: "Warcraft Logs rate limit reached. Please retry shortly."); }
        catch (Exception ex)
        {
            logger.LogError(ex, "Composition detail loading failed report={ReportCode} fight={FightId}", code, fightId);
            return Problem(statusCode: 502, title: "Unable to load this kill's details from Warcraft Logs.");
        }
    }
}
