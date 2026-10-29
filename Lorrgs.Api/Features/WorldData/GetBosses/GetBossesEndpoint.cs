namespace Lorrgs.Api.Features.WorldData.GetBosses;

public static class GetBossesEndpoint
{
    public static RouteHandlerBuilder MapGetBosses(this RouteGroupBuilder group) =>
        group.MapGet("bosses", GetBossesHandler.Handle)
            .WithName("GetBosses")
            .WithSummary("Get all raid bosses");
}
