namespace Lorrgs.Api.Features.WorldData.GetClassSpecs;

public static class GetClassSpecsEndpoint
{
    public static RouteHandlerBuilder MapGetClassSpecs(this RouteGroupBuilder group) =>
        group.MapGet("classes/{classSlug}/specs", GetClassSpecsHandler.Handle)
            .WithName("GetClassSpecs")
            .WithSummary("Get all specs for a specific class");
}
