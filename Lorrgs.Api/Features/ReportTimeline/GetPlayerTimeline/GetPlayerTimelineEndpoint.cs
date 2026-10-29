using Lorrgs.WarcraftLogs.Models;

namespace Lorrgs.Api.Features.ReportTimeline.GetPlayerTimeline;

public static class GetPlayerTimelineEndpoint
{
    public static RouteHandlerBuilder MapGetPlayerTimeline(this RouteGroupBuilder group) =>
        group.MapGet("", ([AsParameters] GetPlayerTimelineRequest request, GetPlayerTimelineHandler handler, CancellationToken cancellationToken) =>
            handler.Handle(request, cancellationToken))
            .WithName("GetPlayerTimeline")
            .WithSummary("Returns real player casts, buff intervals and deaths aligned to fight start.")
            .Produces<Lorrgs.WarcraftLogs.Models.ReportTimeline>()
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(429)
            .ProducesProblem(502);
}
