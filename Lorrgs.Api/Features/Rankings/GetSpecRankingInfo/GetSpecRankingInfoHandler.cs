using System.Net;
using Lorrgs.Api.Shared.Responses;
using Lorrgs.WarcraftLogs.Models;
using Lorrgs.WarcraftLogs.Rankings;
using Microsoft.AspNetCore.Http.HttpResults;
using static Lorrgs.WarcraftLogs.Rankings.RankingsDataService;

namespace Lorrgs.Api.Features.Rankings.GetSpecRankingInfo;

public sealed class GetSpecRankingInfoHandler(ILogger<GetSpecRankingInfoHandler> logger, RankingsDataService data)
{
    public async Task<Results<Ok<SpecRankingData>, BadRequest<ApiErrorResponse>, NotFound<ApiErrorResponse>, JsonHttpResult<ApiErrorResponse>>> Handle(
        GetSpecRankingInfoRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Fetching spec ranking info");

        if (!data.TryResolveSpecRankingQuery(request.SpecSlug, request.BossSlug, request.Edition, request.ZoneId, request.EncounterId, request.ClassName, request.SpecName, out var queryContext, out var queryError))
        {
            return TypedResults.BadRequest(new ApiErrorResponse(queryError));
        }

        var difficultyValue = ResolveDifficultyValue(request.Difficulty, request.DifficultyId);
        if (difficultyValue == null)
        {
            return TypedResults.BadRequest(new ApiErrorResponse("Invalid difficulty. Provide difficultyId or one of: lfr, normal, heroic, mythic."));
        }

        var difficultyLabel = BuildDifficultyLabel(request.Difficulty, difficultyValue.Value);

        if (string.IsNullOrWhiteSpace(request.Metric))
        {
            return TypedResults.BadRequest(new ApiErrorResponse("metric is required. Frontend must provide an explicit metric value."));
        }

        var normalizedMetric = NormalizeMetric(request.Metric);
        if (normalizedMetric == null)
        {
            return TypedResults.BadRequest(new ApiErrorResponse("Invalid metric for CharacterRankingMetricType"));
        }

        if (!TryBuildRankingRequestOptions(request.Page, request.Partition, request.Bracket, request.Size, request.ServerRegion, request.ServerSlug, request.Filter, request.HardModeLevel, request.ExternalBuffs, request.IncludeCombatantInfo, request.IncludeOtherPlayers, out var requestOptions, out var optionsError))
        {
            return TypedResults.BadRequest(new ApiErrorResponse(optionsError));
        }

        try
        {
            var ranking = await data.FetchSpecRankingFromWCL(
                queryContext,
                difficultyValue.Value,
                difficultyLabel,
                normalizedMetric,
                request.Edition,
                requestOptions,
                request.Refresh,
                includeReportDetails: false,
                cancellationToken: cancellationToken);

            if (ranking == null)
            {
                return TypedResults.NotFound(new ApiErrorResponse("Ranking not found"));
            }

            // Return info-only payload without report details.
            ranking.Reports?.Clear();

            return TypedResults.Ok(ranking);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            logger.LogWarning("WarcraftLogs rate limit hit while fetching spec ranking info");
            return TypedResults.Json(new ApiErrorResponse("Warcraft Logs rate limit reached. Please retry shortly."), statusCode: StatusCodes.Status429TooManyRequests);
        }
        catch (Exception ex)
        {
            logger.LogError("Error fetching spec ranking info exceptionType={ExceptionType}", ex.GetType().FullName);
            return TypedResults.Json(new ApiErrorResponse("Failed to fetch ranking info"), statusCode: 500);
        }
    }
}
