using System.Text.Json;
using Lorrgs.Api.Shared.Responses;

namespace Lorrgs.Api.Features.WarcraftLogs.LookupReport;

public static class LookupReportEndpoint
{
    public static RouteHandlerBuilder MapLookupReport(this RouteGroupBuilder group) =>
        group.MapGet("reports/{reportCode}/lookup", ([AsParameters] LookupReportRequest request, LookupReportHandler handler, CancellationToken cancellationToken) =>
            handler.Handle(request, cancellationToken))
            .WithName("LookupReport")
            .WithSummary("Lookup Report")
            .Produces<JsonElement>()
            .Produces<ApiErrorResponse>(400)
            .Produces<ApiErrorResponse>(429)
            .Produces<ApiErrorResponse>(500);
}
