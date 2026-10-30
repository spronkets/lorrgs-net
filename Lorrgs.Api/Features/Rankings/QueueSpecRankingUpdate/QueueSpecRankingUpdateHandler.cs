using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.Rankings.QueueSpecRankingUpdate;

public sealed class QueueSpecRankingUpdateHandler(ILogger<QueueSpecRankingUpdateHandler> logger)
{
    public async Task<Ok<QueueSpecRankingUpdateResponse>> Handle(
        QueueSpecRankingUpdateRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Queuing spec ranking update");

        // TODO: Implement task queue with Azure Service Bus
        // For now, return placeholder response
        return TypedResults.Ok(new QueueSpecRankingUpdateResponse(
            "Task queued for ranking update", Guid.NewGuid().ToString(),
            new QueueSpecRankingUpdateRequest
            {
                SpecSlug = request.SpecSlug, BossSlug = request.BossSlug, Difficulty = request.Difficulty,
                Metric = request.Metric, Limit = request.Limit, Clear = request.Clear
            }));
    }
}
