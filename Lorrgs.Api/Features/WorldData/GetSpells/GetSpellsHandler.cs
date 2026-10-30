using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetSpells;

public static class GetSpellsHandler
{
    public static Ok<GetSpellsResponse> Handle(
        WorldDataService data, ILoggerFactory loggerFactory,
        [FromQuery] string? edition, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GetSpells");
        var spells = data.Spells.OrderBy(s => s.Name).ToList();
        logger.LogInformation("Found {SpellCount} spells", spells.Count);

        return TypedResults.Ok(new GetSpellsResponse(spells));
    }
}
