namespace Lorrgs.Api.Features.ReportTimeline.GetBossTimeline;

public sealed class GetBossTimelineHandler(TimelineLoader loader)
    {
    public async Task<IResult> Handle(
        GetBossTimelineRequest request, CancellationToken cancellationToken = default)
        {
        return await loader.Load(request.ReportCode, request.FightId, null, null, request.Edition, true, cancellationToken, null);
    }
}
