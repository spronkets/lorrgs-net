namespace Lorrgs.Api.Features.WorldData.GetRoles;

public static class GetRolesEndpoint
{
    public static RouteHandlerBuilder MapGetRoles(this RouteGroupBuilder group) =>
        group.MapGet("roles", GetRolesHandler.Handle)
            .WithName("GetRoles")
            .WithSummary("Get all WoW roles (Tank, Healer, Melee DPS, Ranged DPS)");
}
