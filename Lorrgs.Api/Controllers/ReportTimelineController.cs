using System.Net;
using Lorrgs.Api.Models;
using Lorrgs.Api.Services;
using Lorrgs.WarcraftLogs;
using Microsoft.AspNetCore.Mvc;

namespace Lorrgs.Api.Controllers;

[ApiController]
[Route("api/warcraftlogs/reports/{reportCode}/timeline")]
public sealed class ReportTimelineController(
    IReportTimelineService service, ILogger<ReportTimelineController> logger) : ControllerBase
{
    /// <summary>Returns real player casts, buff intervals and deaths aligned to fight start.</summary>
    [HttpGet]
    [ProducesResponseType<ReportTimeline>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Get(string reportCode, [FromQuery] int fightId,
        [FromQuery] int? sourceId, [FromQuery] string? playerName,
        [FromQuery] string edition = "retail", CancellationToken cancellationToken = default,
        [FromQuery] int? specId = null) =>
        await Load(reportCode, fightId, sourceId, playerName, edition, false, cancellationToken, specId);

    /// <summary>Returns real enemy casts and observed phase transitions from a reference fight.</summary>
    [HttpGet("boss")]
    [ProducesResponseType<BossTimeline>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetBoss(string reportCode, [FromQuery] int fightId,
        [FromQuery] string edition = "retail", CancellationToken cancellationToken = default) =>
        await Load(reportCode, fightId, null, null, edition, true, cancellationToken, null);

    private async Task<IActionResult> Load(string reportCode, int fightId, int? sourceId,
        string? playerName, string edition, bool boss, CancellationToken cancellationToken, int? specId)
    {
        if (!IsValid(reportCode, fightId, sourceId, playerName, edition, boss))
        {
            logger.LogWarning("Invalid timeline request report={ReportCode} fight={FightId} edition={Edition}",
                reportCode, fightId, edition);
            return Problem(statusCode: 400, detail: "Provide a valid report, fight, player and edition.");
        }
        try
        {
            if (boss)
                return Ok(await service.LoadBossAsync(reportCode, fightId, edition, false, cancellationToken)
                    ?? throw new InvalidOperationException("The boss timeline could not be loaded."));
            return Ok(await service.LoadAsync(reportCode, fightId, sourceId, playerName, edition,
                cancellationToken, specId));
        }
        catch (WarcraftLogsReportPrivateException ex)
        {
            logger.LogWarning(ex, "Timeline report is private report={ReportCode} fight={FightId}", reportCode, fightId);
            return Problem(statusCode: 403, detail: "This Warcraft Logs report is private. Make it public to load its timeline.");
        }
        catch (WarcraftLogsReportNotFoundException ex)
        {
            logger.LogWarning(ex, "Timeline report missing report={ReportCode} fight={FightId}", reportCode, fightId);
            return Problem(statusCode: 404, detail: "This Warcraft Logs report was not found; it may have been deleted or its code may be invalid.");
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogWarning(ex, "Timeline report or player missing report={ReportCode} fight={FightId}", reportCode, fightId);
            return Problem(statusCode: 404, detail: ex.Message);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            logger.LogWarning(ex, "Timeline rate limited report={ReportCode} fight={FightId}", reportCode, fightId);
            return Problem(statusCode: 429, detail: "Warcraft Logs rate limit reached. Retry after the cooldown.");
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or System.Text.Json.JsonException)
        {
            logger.LogError(ex, "Timeline loading failed report={ReportCode} fight={FightId} edition={Edition}", reportCode, fightId, edition);
            return Problem(statusCode: 502, detail: "Unable to load this timeline from Warcraft Logs.");
        }
    }

    private static bool IsValid(string reportCode, int fightId, int? sourceId, string? playerName, string edition, bool boss) =>
        !string.IsNullOrWhiteSpace(reportCode) && reportCode.Length <= 32 &&
        reportCode.All(char.IsAsciiLetterOrDigit) && fightId > 0 &&
        (boss || sourceId is > 0 || !string.IsNullOrWhiteSpace(playerName)) &&
        new[] { "retail", "anniversary", "mistsofpandaria", "era" }.Contains(edition);
}
