using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.Rankings.QueueCompRankingUpdate;

public sealed class QueueCompRankingUpdateHandler(ILogger<QueueCompRankingUpdateHandler> logger)
{
    public async Task<Ok<QueueCompRankingUpdateResponse>> Handle(
        QueueCompRankingUpdateRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Queuing comp ranking update");

        // TODO: Implement task queue with Azure Service Bus
        return TypedResults.Ok(new QueueCompRankingUpdateResponse(
            "Task queued for comp ranking update", Guid.NewGuid().ToString(),
            new QueueCompRankingUpdateRequest { BossSlug = request.BossSlug, Limit = request.Limit, Clear = request.Clear }));
    }
}
