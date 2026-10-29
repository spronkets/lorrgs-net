using Lorrgs.Api.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Lorrgs.Api.Features.WorldData.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetRoleSpecs;

public static class GetRoleSpecsHandler
{
    public static Results<Ok<GetRoleSpecsResponse>, NotFound<ApiErrorResponse>> Handle(
        WorldDataService data, ILoggerFactory loggerFactory,
        [AsParameters] GetRoleSpecsRequest request, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GetRoleSpecs");
        logger.LogInformation("Fetching specs for role: {RoleSlug}", request.RoleSlug);
        var role = data.Roles.FirstOrDefault(r => r.NameSlug == request.RoleSlug);
        if (role == null)
        {
            return TypedResults.NotFound(new ApiErrorResponse($"Role not found: {request.RoleSlug}"));
        }

        var specs = data.GetSpecsByRole(role.Id)
            .OrderBy(s => s.FullName)
            .ToList();

        return TypedResults.Ok(new GetRoleSpecsResponse(specs));
    }
}
