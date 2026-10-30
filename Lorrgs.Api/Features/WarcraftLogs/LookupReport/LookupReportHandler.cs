using System.Net;
using System.Text.Json;
using Lorrgs.Api.Shared.Responses;
using Lorrgs.WarcraftLogs.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WarcraftLogs.LookupReport;

public sealed class LookupReportHandler(ILogger<LookupReportHandler> logger, ReportLookupService reports)
{
    public async Task<Results<Ok<JsonElement>, BadRequest<ApiErrorResponse>, JsonHttpResult<ApiErrorResponse>>> Handle(
        LookupReportRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ReportCode))
        {
            return TypedResults.BadRequest(new ApiErrorResponse("reportCode is required"));
        }

        try
        {
            var data = await reports.LoadAsync(request.ReportCode, cancellationToken);
            if (data.TryGetProperty("reportData", out var reportData) &&
                reportData.TryGetProperty("report", out var report) && report.ValueKind == JsonValueKind.Object)
                logger.LogInformation("Retrieved report metadata report={ReportCode} title={ReportTitle}",
                    report.TryGetProperty("code", out var code) ? code.GetString() : null,
                    report.TryGetProperty("title", out var title) ? title.GetString() : null);
            return TypedResults.Ok(data);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            logger.LogWarning("WarcraftLogs rate limit hit while looking up report");
            return TypedResults.Json(new ApiErrorResponse("Warcraft Logs rate limit reached. Please retry shortly."), statusCode: StatusCodes.Status429TooManyRequests);
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to lookup report exceptionType={ExceptionType}", ex.GetType().FullName);
            return TypedResults.Json(new ApiErrorResponse("Failed to lookup report metadata"), statusCode: 500);
        }
    }
}
