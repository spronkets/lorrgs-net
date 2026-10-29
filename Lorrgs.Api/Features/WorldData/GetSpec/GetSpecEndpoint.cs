namespace Lorrgs.Api.Features.WorldData.GetSpec;

public static class GetSpecEndpoint
{
    public static RouteHandlerBuilder MapGetSpec(this RouteGroupBuilder group) =>
        group.MapGet("specs/{specSlug}", GetSpecHandler.Handle)
            .WithName("GetSpec")
            .WithSummary("Get a specific spec by its slug (e.g., frost-mage)");
}
