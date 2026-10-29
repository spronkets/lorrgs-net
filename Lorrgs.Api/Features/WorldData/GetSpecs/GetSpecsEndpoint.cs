namespace Lorrgs.Api.Features.WorldData.GetSpecs;

public static class GetSpecsEndpoint
{
    public static RouteHandlerBuilder MapGetSpecs(this RouteGroupBuilder group) =>
        group.MapGet("specs", GetSpecsHandler.Handle)
            .WithName("GetSpecs")
            .WithSummary("Get all WoW specs, optionally filtered by role or class");
}
