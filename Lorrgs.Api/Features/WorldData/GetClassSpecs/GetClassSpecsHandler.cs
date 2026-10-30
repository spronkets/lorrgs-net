using Lorrgs.Api.Shared.Responses;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetClassSpecs;

public static class GetClassSpecsHandler
{
    public static Results<Ok<GetClassSpecsResponse>, NotFound<ApiErrorResponse>> Handle(
        WorldDataService data, ILoggerFactory loggerFactory,
        [AsParameters] GetClassSpecsRequest request, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GetClassSpecs");
        var wowClass = data.Classes.FirstOrDefault(c => c.NameSlug == request.ClassSlug);
        if (wowClass == null)
        {
            logger.LogWarning("Class not found");
            return TypedResults.NotFound(new ApiErrorResponse($"Class not found: {request.ClassSlug}"));
        }

        var specs = data.GetSpecsByClass(wowClass.Id)
            .OrderBy(s => s.Name)
            .ToList();

        logger.LogInformation("Found {SpecCount} specs for class: {ClassId} {ClassName}",
            specs.Count, wowClass.Id, wowClass.Name);
        return TypedResults.Ok(new GetClassSpecsResponse(specs));
    }
}
