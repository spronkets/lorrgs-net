namespace Lorrgs.Api.Features.WorldData.GetSpell;

public static class GetSpellEndpoint
{
    public static RouteHandlerBuilder MapGetSpell(this RouteGroupBuilder group) =>
        group.MapGet("spells/{spellId}", GetSpellHandler.Handle)
            .WithName("GetSpell")
            .WithSummary("Get a specific spell by ID");
}
