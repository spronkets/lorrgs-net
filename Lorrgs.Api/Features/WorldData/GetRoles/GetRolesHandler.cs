using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetRoles;

public static class GetRolesHandler
{
    public static Ok<GetRolesResponse> Handle(
        WorldDataService data, ILoggerFactory loggerFactory,
        [FromQuery] string? edition, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GetRoles");
        var roles = data.Roles.OrderBy(r => r.Id).ToList();
        logger.LogInformation("Found {RoleCount} roles", roles.Count);

        return TypedResults.Ok(new GetRolesResponse(roles));
    }
}
