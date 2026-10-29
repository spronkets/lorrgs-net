using System.Net;
using Lorrgs.Api.Shared.Responses;
using Lorrgs.WarcraftLogs.Models;
using Lorrgs.WarcraftLogs.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WarcraftLogs.AnalyzeRotation;

public sealed class AnalyzeRotationHandler(ILogger<AnalyzeRotationHandler> logger, RotationAnalysisService rotationAnalysisService)
{
    public async Task<Results<Ok<RotationAnalysisResponse>, BadRequest<ApiErrorResponse>, NotFound<ApiErrorResponse>, JsonHttpResult<ApiErrorResponse>, JsonHttpResult<ApiErrorDetailResponse>>> Handle(
        RotationAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await rotationAnalysisService.AnalyzeAsync(new RotationAnalysisOptions
            {
                ReportCode = request.ReportCode, FightId = request.FightId, SourceId = request.SourceId,
                PlayerName = request.PlayerName, ParsePercentile = request.ParsePercentile,
                ItemLevel = request.ItemLevel, EventLimit = request.EventLimit
            }, cancellationToken);
            return TypedResults.Ok(new RotationAnalysisResponse
            {
                ReportCode = result.ReportCode,
                FightId = result.FightId,
                FightName = result.FightName,
                PlayerName = result.PlayerName,
                SourceId = result.SourceId,
                RoleHint = result.RoleHint,
                SpecHint = result.SpecHint,
                GameExpansionHint = result.GameExpansionHint,
                ParsePercentile = result.ParsePercentile,
                ItemLevel = result.ItemLevel,
                FightDurationSeconds = result.FightDurationSeconds,
                CastsPerMinute = result.CastsPerMinute,
                Verdict = result.Verdict,
                Score = result.Score,
                Notes = result.Notes,
                AbilitySummary = result.AbilitySummary,
                RotationTimeline = result.RotationTimeline,
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest(new ApiErrorResponse(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.NotFound(new ApiErrorResponse(ex.Message));
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            logger.LogWarning(ex, "WarcraftLogs rate limit hit during rotation analysis for report {ReportCode}", request.ReportCode);
            return TypedResults.Json(new ApiErrorResponse("Warcraft Logs rate limit reached. Please retry shortly."), statusCode: StatusCodes.Status429TooManyRequests);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Rotation analysis failed for report {ReportCode}", request.ReportCode);
            return TypedResults.Json(new ApiErrorDetailResponse("Rotation analysis failed", ex.Message), statusCode: 500);
        }
    }
}
