using Lorrgs.Api.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
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
        logger.LogInformation("Fetching season: {SeasonSlug}", request.SeasonSlug);
        var season = data.GetSeason(request.SeasonSlug);
        if (season == null)
        {
            return TypedResults.NotFound(new ApiErrorResponse($"Season not found: {request.SeasonSlug}"));
        }

        return TypedResults.Ok(season);
    }
}
