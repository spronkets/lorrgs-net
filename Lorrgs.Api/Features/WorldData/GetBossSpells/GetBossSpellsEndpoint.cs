namespace Lorrgs.Api.Features.WorldData.GetBossSpells;

public static class GetBossSpellsEndpoint
{
    public static RouteHandlerBuilder MapGetBossSpells(this RouteGroupBuilder group) =>
        group.MapGet("bosses/{bossSlug}/spells", GetBossSpellsHandler.Handle)
            .WithName("GetBossSpells")
            .WithSummary("Get all spells for a boss (boss abilities tracked in timeline)");
}
