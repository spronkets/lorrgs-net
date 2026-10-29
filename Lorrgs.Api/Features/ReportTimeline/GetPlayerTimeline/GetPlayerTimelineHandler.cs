namespace Lorrgs.Api.Features.ReportTimeline.GetPlayerTimeline;

public sealed class GetPlayerTimelineHandler(TimelineLoader loader)
    {
    public async Task<IResult> Handle(
        GetPlayerTimelineRequest request, CancellationToken cancellationToken = default)
        {
        return await loader.Load(request.ReportCode, request.FightId, request.SourceId, request.PlayerName, request.Edition, false, cancellationToken, request.SpecId);
    }
}
