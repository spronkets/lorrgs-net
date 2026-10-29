using Lorrgs.Api.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Lorrgs.Api.Features.WorldData.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetClassSpecs;

public static class GetClassSpecsHandler
{
    public static Results<Ok<GetClassSpecsResponse>, NotFound<ApiErrorResponse>> Handle(
        WorldDataService data, ILoggerFactory loggerFactory,
        [AsParameters] GetClassSpecsRequest request, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GetClassSpecs");
        logger.LogInformation("Fetching specs for class: {ClassSlug}", request.ClassSlug);
        var wowClass = data.Classes.FirstOrDefault(c => c.NameSlug == request.ClassSlug);
        if (wowClass == null)
        {
            return TypedResults.NotFound(new ApiErrorResponse($"Class not found: {request.ClassSlug}"));
        }

        var specs = data.GetSpecsByClass(wowClass.Id)
            .OrderBy(s => s.Name)
            .ToList();

        return TypedResults.Ok(new GetClassSpecsResponse(specs));
    }
}
