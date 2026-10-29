using Lorrgs.Api.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
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
        logger.LogInformation("Fetching spec: {SpecSlug}", request.SpecSlug);
        var spec = data.GetSpec(request.SpecSlug);
        if (spec == null)
        {
            return TypedResults.NotFound(new ApiErrorResponse($"Spec not found: {request.SpecSlug}"));
        }

        return TypedResults.Ok(spec);
    }
}
