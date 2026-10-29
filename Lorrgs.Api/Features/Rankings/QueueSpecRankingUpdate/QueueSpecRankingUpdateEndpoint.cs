namespace Lorrgs.Api.Features.Rankings.QueueSpecRankingUpdate;

public static class QueueSpecRankingUpdateEndpoint
{
    public static RouteHandlerBuilder MapQueueSpecRankingUpdate(this RouteGroupBuilder group) =>
        group.MapPost("spec/queue", ([AsParameters] QueueSpecRankingUpdateRequest request, QueueSpecRankingUpdateHandler handler, CancellationToken cancellationToken) =>
            handler.Handle(request, cancellationToken))
            .WithName("QueueSpecRankingUpdate")
            .WithSummary("Queue a background job to update spec rankings. Returns immediately with a task ID for tracking progress.")
            .Produces<QueueSpecRankingUpdateResponse>();
}
