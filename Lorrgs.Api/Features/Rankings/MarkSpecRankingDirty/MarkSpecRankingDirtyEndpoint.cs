namespace Lorrgs.Api.Features.Rankings.MarkSpecRankingDirty;

public static class MarkSpecRankingDirtyEndpoint
{
    public static RouteHandlerBuilder MapMarkSpecRankingDirty(this RouteGroupBuilder group) =>
        group.MapPatch("spec/dirty", ([AsParameters] MarkSpecRankingDirtyRequest request, MarkSpecRankingDirtyHandler handler, CancellationToken cancellationToken) =>
            handler.Handle(request, cancellationToken))
            .WithName("MarkSpecRankingDirty")
            .WithSummary("Queue a background job to update comp rankings.")
            .Produces<MarkSpecRankingDirtyResponse>();
}
