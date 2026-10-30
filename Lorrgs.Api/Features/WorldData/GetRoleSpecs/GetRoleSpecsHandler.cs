using Lorrgs.Api.Shared.Responses;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetRoleSpecs;

public static class GetRoleSpecsHandler
{
    public static Results<Ok<GetRoleSpecsResponse>, NotFound<ApiErrorResponse>> Handle(
        WorldDataService data, ILoggerFactory loggerFactory,
        [AsParameters] GetRoleSpecsRequest request, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GetRoleSpecs");
        var role = data.Roles.FirstOrDefault(r => r.NameSlug == request.RoleSlug);
        if (role == null)
        {
            logger.LogWarning("Role not found");
            return TypedResults.NotFound(new ApiErrorResponse($"Role not found: {request.RoleSlug}"));
        }

        var specs = data.GetSpecsByRole(role.Id)
            .OrderBy(s => s.FullName)
            .ToList();

        logger.LogInformation("Found {SpecCount} specs for role: {RoleId} {RoleName}",
            specs.Count, role.Id, role.Name);
        return TypedResults.Ok(new GetRoleSpecsResponse(specs));
    }
}
