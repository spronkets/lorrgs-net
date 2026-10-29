namespace Lorrgs.Api.Features.WorldData.GetSpecSpells;

public static class GetSpecSpellsEndpoint
{
    public static RouteHandlerBuilder MapGetSpecSpells(this RouteGroupBuilder group) =>
        group.MapGet("specs/{specSlug}/spells", GetSpecSpellsHandler.Handle)
            .WithName("GetSpecSpells")
            .WithSummary("Get all spells for a spec (abilities tracked in timeline)");
}
