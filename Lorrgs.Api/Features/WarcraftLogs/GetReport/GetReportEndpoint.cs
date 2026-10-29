namespace Lorrgs.Api.Features.WarcraftLogs.GetReport;

public static class GetReportEndpoint
{
    public static RouteHandlerBuilder MapGetReport(this RouteGroupBuilder group) =>
        group.MapGet("reports/{reportId}", ([AsParameters] GetReportRequest request, GetReportHandler handler, CancellationToken cancellationToken) =>
            handler.Handle(request, cancellationToken))
            .WithName("GetReport")
            .WithSummary("Get Report")
            .Produces<GetReportResponse>();
}
