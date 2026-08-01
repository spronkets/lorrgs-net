using Lorrgs.Api.Models;
using Lorrgs.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Lorrgs.Api.Controllers;

[ApiController]
[Route("api/raid-catalog")]
public class RaidCatalogController(ILogger<RaidCatalogController> logger, RaidCatalogClient raidCatalogClient) : ControllerBase
{
    private readonly ILogger<RaidCatalogController> _logger = logger;
    private readonly RaidCatalogClient _raidCatalogClient = raidCatalogClient;

    [HttpGet]
    public async Task<IActionResult> GetCatalog()
    {
        _logger.LogInformation("Fetching raid catalog");

        RaidCatalog catalog = await _raidCatalogClient.GetCatalogAsync();
        return Ok(catalog);
    }
}