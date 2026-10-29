using Microsoft.AspNetCore.Mvc;

namespace Lorrgs.Api.Features.WarcraftLogs.LookupReport;

/// <summary>Parameters for LookupReport.</summary>
public sealed record LookupReportRequest(
    [property: FromRoute(Name = "reportCode")] string ReportCode);
