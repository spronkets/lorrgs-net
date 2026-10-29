namespace Lorrgs.Api.Features.WarcraftLogs.GetReport;

/// <summary>Summary information for the requested report.</summary>
public sealed record GetReportResponse(string ReportId, string Title, string FightLength, string Owner);
