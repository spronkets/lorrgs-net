using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetSeasons;

public static class GetSeasonsHandler
{
    public static Ok<GetSeasonsResponse> Handle(
        WorldDataService data, ILoggerFactory loggerFactory,
        [FromQuery] string? edition, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GetSeasons");
        var seasons = data.Seasons.OrderBy(s => s.StartDate).ToList();
        logger.LogInformation("Found {SeasonCount} seasons", seasons.Count);

        return TypedResults.Ok(new GetSeasonsResponse(seasons));
    }
}
