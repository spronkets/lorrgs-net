namespace Lorrgs.Api.Features.Rankings.QueueCompRankingUpdate;

public static class QueueCompRankingUpdateEndpoint
{
    public static RouteHandlerBuilder MapQueueCompRankingUpdate(this RouteGroupBuilder group) =>
        group.MapPost("comp/queue", ([AsParameters] QueueCompRankingUpdateRequest request, QueueCompRankingUpdateHandler handler, CancellationToken cancellationToken) =>
            handler.Handle(request, cancellationToken))
            .WithName("QueueCompRankingUpdate")
            .WithSummary("Queue a background job to update spec rankings. Returns immediately with a task ID for tracking progress.")
            .Produces<QueueCompRankingUpdateResponse>();
}
