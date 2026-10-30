using Lorrgs.Api.Shared.Responses;
using Lorrgs.Api.Features.WorldData.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetSpec;

public static class GetSpecHandler
{
    public static Results<Ok<WowSpec>, NotFound<ApiErrorResponse>> Handle(
        WorldDataService data, ILoggerFactory loggerFactory,
        [AsParameters] GetSpecRequest request, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GetSpec");
        var spec = data.GetSpec(request.SpecSlug);
        if (spec == null)
        {
            logger.LogWarning("Spec not found");
            return TypedResults.NotFound(new ApiErrorResponse($"Spec not found: {request.SpecSlug}"));
        }

        logger.LogInformation("Found spec: {SpecId} {SpecName}", spec.Id, spec.FullName);
        return TypedResults.Ok(spec);
    }
}
