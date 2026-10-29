namespace Lorrgs.Api.Features.WorldData.GetRoleSpecs;

public static class GetRoleSpecsEndpoint
{
    public static RouteHandlerBuilder MapGetRoleSpecs(this RouteGroupBuilder group) =>
        group.MapGet("roles/{roleSlug}/specs", GetRoleSpecsHandler.Handle)
            .WithName("GetRoleSpecs")
            .WithSummary("Get all specs for a specific role");
}
