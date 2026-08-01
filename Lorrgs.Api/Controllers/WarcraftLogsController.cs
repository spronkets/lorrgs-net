using Microsoft.AspNetCore.Mvc;
using Lorrgs.Api.Models;
using Lorrgs.Api.Services;

namespace Lorrgs.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WarcraftLogsController(
        ILogger<WarcraftLogsController> logger,
        RotationAnalysisService rotationAnalysisService,
        WarcraftLogsClient warcraftLogsClient) : ControllerBase
{
    private readonly ILogger<WarcraftLogsController> _logger = logger;
        private readonly RotationAnalysisService _rotationAnalysisService = rotationAnalysisService;
        private readonly WarcraftLogsClient _warcraftLogsClient = warcraftLogsClient;

        private const string ReportLookupQuery = """
                query ReportLookup($code: String!) {
                    reportData {
                        report(code: $code) {
                            title
                            gameZone {
                                id
                                name
                            }
                            fights {
                                id
                                name
                                kill
                                difficulty
                            }
                            masterData {
                                actors(type: "Player") {
                                    id
                                    name
                                    subType
                                }
                            }
                        }
                    }
                }
        """;

    [HttpGet("seasons")]
    public IActionResult GetSeasons()
    {
        var seasons = new[]
        {
            new { id = "season-1", name = "Season 1", expansion = "The War Within" },
            new { id = "season-2", name = "Season 2", expansion = "The War Within" }
        };

        return Ok(seasons);
    }

    [HttpGet("rankings")]
    public IActionResult GetRankings([FromQuery] string? season = null)
    {
        var rankings = new[]
        {
            new { id = 1, name = "Abyssal", className = "Mage", spec = "Fire", percentile = 99.7, season = season ?? "current" },
            new { id = 2, name = "Stormcaller", className = "Shaman", spec = "Elemental", percentile = 98.2, season = season ?? "current" }
        };

        return Ok(rankings);
    }

    [HttpGet("reports/{reportId}")]
    public IActionResult GetReport(string reportId)
    {
        _logger.LogInformation("Requested report {ReportId}", reportId);

        return Ok(new
        {
            reportId,
            title = "Example WarcraftLogs report",
            fightLength = "12m 34s",
            owner = "LorrgsNET"
        });
    }

    [HttpGet("reports/{reportCode}/lookup")]
    public async Task<IActionResult> LookupReport(string reportCode)
    {
        if (string.IsNullOrWhiteSpace(reportCode))
        {
            return BadRequest(new { error = "reportCode is required" });
        }

        try
        {
            var data = await _warcraftLogsClient.QueryGraphQLAsync(
                ReportLookupQuery,
                new { code = reportCode.Trim() }
            );

            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to lookup report {ReportCode}", reportCode);
            return StatusCode(500, new { error = "Failed to lookup report metadata" });
        }
    }

    [HttpPost("rotation/analyze")]
    public async Task<IActionResult> AnalyzeRotation([FromBody] RotationAnalysisRequest request)
    {
        try
        {
            var result = await _rotationAnalysisService.AnalyzeAsync(request);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rotation analysis failed for report {ReportCode}", request.ReportCode);
            return StatusCode(500, new { error = "Rotation analysis failed", detail = ex.Message });
        }
    }
}
