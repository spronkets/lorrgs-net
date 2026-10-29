using Microsoft.AspNetCore.Mvc;

namespace Lorrgs.Api.Features.WarcraftLogs.GetReport;

/// <summary>Parameters for GetReport.</summary>
public sealed record GetReportRequest(
    [property: FromRoute(Name = "reportId")] string ReportId);
