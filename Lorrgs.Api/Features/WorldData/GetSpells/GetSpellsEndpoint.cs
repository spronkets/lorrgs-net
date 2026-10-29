namespace Lorrgs.Api.Features.WorldData.GetSpells;

public static class GetSpellsEndpoint
{
    public static RouteHandlerBuilder MapGetSpells(this RouteGroupBuilder group) =>
        group.MapGet("spells", GetSpellsHandler.Handle)
            .WithName("GetSpells")
            .WithSummary("Get all WoW spells");
}
