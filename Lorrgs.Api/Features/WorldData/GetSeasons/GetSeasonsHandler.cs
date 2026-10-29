using Microsoft.AspNetCore.Mvc;
using Lorrgs.Api.Features.WorldData.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetSeasons;

public static class GetSeasonsHandler
{
    public static Ok<GetSeasonsResponse> Handle(
        WorldDataService data, ILoggerFactory loggerFactory,
        [FromQuery] string? edition, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GetSeasons");
        logger.LogInformation("Fetching all seasons");
        var seasons = data.Seasons.OrderBy(s => s.StartDate).ToList();

        return TypedResults.Ok(new GetSeasonsResponse(seasons));
    }
}
