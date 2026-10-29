namespace Lorrgs.Api.Features.WorldData.GetClasses;

public static class GetClassesEndpoint
{
    public static RouteHandlerBuilder MapGetClasses(this RouteGroupBuilder group) =>
        group.MapGet("classes", GetClassesHandler.Handle)
            .WithName("GetClasses")
            .WithSummary("Get all WoW classes (Warrior, Mage, etc.)");
}
