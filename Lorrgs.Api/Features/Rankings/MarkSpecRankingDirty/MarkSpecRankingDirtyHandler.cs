using Lorrgs.Api.Shared.Responses;
using Lorrgs.WarcraftLogs.Rankings;
using Microsoft.AspNetCore.Http.HttpResults;
using static Lorrgs.WarcraftLogs.Rankings.RankingsDataService;

namespace Lorrgs.Api.Features.Rankings.MarkSpecRankingDirty;

public sealed class MarkSpecRankingDirtyHandler(ILogger<MarkSpecRankingDirtyHandler> logger, RankingsDataService data)
{
    public async Task<Results<Ok<MarkSpecRankingDirtyResponse>, BadRequest<ApiErrorResponse>>> Handle(
        MarkSpecRankingDirtyRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Marking spec ranking dirty");

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

        return TypedResults.Ok(new MarkSpecRankingDirtyResponse(
            request.Dirty
                ? "Ranking refresh acknowledged. API response caching is disabled; use refresh=true to bypass WarcraftLogs response cache."
                : "Ranking marked clean",
            request.SpecSlug,
            request.BossSlug,
            difficultyLabel,
            normalizedMetric));
    }
}
