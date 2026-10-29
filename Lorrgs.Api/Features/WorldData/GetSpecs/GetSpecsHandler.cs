using Microsoft.AspNetCore.Mvc;
using Lorrgs.Api.Features.WorldData.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetSpecs;

public static class GetSpecsHandler
{
    public static Ok<GetSpecsResponse> Handle(
        WorldDataService data, ILoggerFactory loggerFactory,
        [AsParameters] GetSpecsRequest request, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GetSpecs");
        logger.LogInformation("Fetching specs: roleId={RoleId}, classId={ClassId}", request.RoleId, request.ClassId);
        var specs = data.Specs.AsEnumerable();

        if (request.RoleId.HasValue)
            specs = specs.Where(s => s.RoleId == request.RoleId);

        if (request.ClassId.HasValue)
            specs = specs.Where(s => s.ClassId == request.ClassId);

        var result = specs.OrderBy(s => s.FullName).ToList();

        return TypedResults.Ok(new GetSpecsResponse(result));
    }
}
