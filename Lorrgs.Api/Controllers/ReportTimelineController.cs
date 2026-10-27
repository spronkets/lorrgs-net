using System.Net;
using Lorrgs.Api.Models;
using Lorrgs.Api.Services;
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
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Get(string reportCode, [FromQuery] int fightId,
        [FromQuery] int? sourceId, [FromQuery] string? playerName,
        [FromQuery] string edition = "retail", CancellationToken cancellationToken = default)
    {
        if (!IsValid(reportCode, fightId, sourceId, playerName, edition))
        {
            logger.LogWarning("Invalid timeline request report={ReportCode} fight={FightId} edition={Edition}",
                reportCode, fightId, edition);
            return Problem(statusCode: 400, detail: "Provide a valid report, fight, player and edition.");
        }
        try
        {
            return Ok(await service.LoadAsync(reportCode, fightId, sourceId, playerName, edition, cancellationToken));
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

    private static bool IsValid(string reportCode, int fightId, int? sourceId, string? playerName, string edition) =>
        !string.IsNullOrWhiteSpace(reportCode) && reportCode.Length <= 32 &&
        reportCode.All(char.IsAsciiLetterOrDigit) && fightId > 0 &&
        (sourceId is > 0 || !string.IsNullOrWhiteSpace(playerName)) &&
        new[] { "retail", "anniversary", "mistsofpandaria", "era" }.Contains(edition);
}
