using Lorrgs.Api.Shared.Responses;
using Lorrgs.Api.Features.WorldData.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetSeason;

public static class GetSeasonHandler
{
    public static Results<Ok<Season>, NotFound<ApiErrorResponse>> Handle(
        WorldDataService data, ILoggerFactory loggerFactory,
        [AsParameters] GetSeasonRequest request, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GetSeason");
        var season = data.GetSeason(request.SeasonSlug);
        if (season == null)
        {
            logger.LogWarning("Season not found");
            return TypedResults.NotFound(new ApiErrorResponse($"Season not found: {request.SeasonSlug}"));
        }

        logger.LogInformation("Found season: {SeasonId} {SeasonName}", season.Id, season.Name);
        return TypedResults.Ok(season);
    }
}
