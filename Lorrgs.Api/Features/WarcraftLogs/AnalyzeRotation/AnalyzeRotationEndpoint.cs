using Lorrgs.Api.Shared.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Lorrgs.Api.Features.WarcraftLogs.AnalyzeRotation;

public static class AnalyzeRotationEndpoint
{
    public static RouteHandlerBuilder MapAnalyzeRotation(this RouteGroupBuilder group) =>
        group.MapPost("rotation/analyze", ([FromBody] RotationAnalysisRequest request, AnalyzeRotationHandler handler, CancellationToken cancellationToken) =>
            handler.Handle(request, cancellationToken))
            .WithName("AnalyzeRotation")
            .WithSummary("Analyze Rotation")
            .Produces<RotationAnalysisResponse>()
            .Produces<ApiErrorResponse>(400)
            .Produces<ApiErrorResponse>(404)
            .Produces<ApiErrorResponse>(429)
            .Produces<ApiErrorDetailResponse>(500);
}
