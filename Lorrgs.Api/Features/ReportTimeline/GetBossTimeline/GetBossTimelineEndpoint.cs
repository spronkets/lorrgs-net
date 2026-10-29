using Lorrgs.WarcraftLogs.Models;

namespace Lorrgs.Api.Features.ReportTimeline.GetBossTimeline;

public static class GetBossTimelineEndpoint
{
    public static RouteHandlerBuilder MapGetBossTimeline(this RouteGroupBuilder group) =>
        group.MapGet("boss", ([AsParameters] GetBossTimelineRequest request, GetBossTimelineHandler handler, CancellationToken cancellationToken) =>
            handler.Handle(request, cancellationToken))
            .WithName("GetBossTimeline")
            .WithSummary("Returns real enemy casts and observed phase transitions from a reference fight.")
            .Produces<BossTimeline>()
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(429)
            .ProducesProblem(502);
}
