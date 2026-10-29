using Microsoft.AspNetCore.Mvc;
using Lorrgs.Api.Features.WorldData.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetRoles;

public static class GetRolesHandler
{
    public static Ok<GetRolesResponse> Handle(
        WorldDataService data, ILoggerFactory loggerFactory,
        [FromQuery] string? edition, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GetRoles");
        logger.LogInformation("Fetching all roles");
        var roles = data.Roles.OrderBy(r => r.Id).ToList();

        return TypedResults.Ok(new GetRolesResponse(roles));
    }
}
