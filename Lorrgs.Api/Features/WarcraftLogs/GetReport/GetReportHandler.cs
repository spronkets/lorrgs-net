using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WarcraftLogs.GetReport;

public sealed class GetReportHandler(ILogger<GetReportHandler> logger)
{
    public Ok<GetReportResponse> Handle(GetReportRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Requested example report");

        return TypedResults.Ok(new GetReportResponse(
            request.ReportId, "Example WarcraftLogs report", "12m 34s", "LorrgsNET"));
    }
}
