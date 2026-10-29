using Microsoft.AspNetCore.Mvc;
using Lorrgs.Api.Features.WorldData.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetSpells;

public static class GetSpellsHandler
{
    public static Ok<GetSpellsResponse> Handle(
        WorldDataService data, ILoggerFactory loggerFactory,
        [FromQuery] string? edition, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GetSpells");
        logger.LogInformation("Fetching all spells");
        var spells = data.Spells.OrderBy(s => s.Name).ToList();

        return TypedResults.Ok(new GetSpellsResponse(spells));
    }
}
