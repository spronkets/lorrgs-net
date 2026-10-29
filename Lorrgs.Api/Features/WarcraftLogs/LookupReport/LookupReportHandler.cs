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

            return TypedResults.Ok(data);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            logger.LogWarning(ex, "WarcraftLogs rate limit hit while looking up report {ReportCode}", request.ReportCode);
            return TypedResults.Json(new ApiErrorResponse("Warcraft Logs rate limit reached. Please retry shortly."), statusCode: StatusCodes.Status429TooManyRequests);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to lookup report {ReportCode}", request.ReportCode);
            return TypedResults.Json(new ApiErrorResponse("Failed to lookup report metadata"), statusCode: 500);
        }
    }
}
