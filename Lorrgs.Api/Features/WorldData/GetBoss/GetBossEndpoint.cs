namespace Lorrgs.Api.Features.WorldData.GetBoss;

public static class GetBossEndpoint
{
    public static RouteHandlerBuilder MapGetBoss(this RouteGroupBuilder group) =>
        group.MapGet("bosses/{bossSlug}", GetBossHandler.Handle)
            .WithName("GetBoss")
            .WithSummary("Get a specific boss by slug");
}
