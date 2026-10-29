using Lorrgs.Api.Features.WorldData.GetBoss;
using Lorrgs.Api.Features.WorldData.GetBosses;
using Lorrgs.Api.Features.WorldData.GetBossSpells;
using Lorrgs.Api.Features.WorldData.GetClasses;
using Lorrgs.Api.Features.WorldData.GetClassSpecs;
using Lorrgs.Api.Features.WorldData.GetRoles;
using Lorrgs.Api.Features.WorldData.GetRoleSpecs;
using Lorrgs.Api.Features.WorldData.GetSeason;
using Lorrgs.Api.Features.WorldData.GetSeasons;
using Lorrgs.Api.Features.WorldData.GetSpec;
using Lorrgs.Api.Features.WorldData.GetSpecs;
using Lorrgs.Api.Features.WorldData.GetSpecSpells;
using Lorrgs.Api.Features.WorldData.GetSpell;
using Lorrgs.Api.Features.WorldData.GetSpells;
using Lorrgs.Api.Features.WorldData.GetTrinket;
using Lorrgs.Api.Features.WorldData.GetTrinkets;
using Lorrgs.Api.Features.WorldData.GetZone;
using Lorrgs.Api.Features.WorldData.GetZoneBosses;
using Lorrgs.Api.Features.WorldData.GetZones;
using Lorrgs.Api.Shared.Responses;
using Lorrgs.WarcraftLogs.ReferenceData;

namespace Lorrgs.Api.Features.WorldData;

public static class WorldDataFeature
{
    public static IServiceCollection AddWorldData(this IServiceCollection services)
    {
        services.AddSingleton(WorldDataService.Instance);
        services.AddSingleton<IWarcraftReferenceData, WarcraftReferenceData>();
        return services;
    }

    public static RouteGroupBuilder MapWorldData(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/WorldData")
            .WithTags("WorldData")
            .WithMetadata(new ProducesResponseTypeMetadata(
                StatusCodes.Status400BadRequest, typeof(ApiErrorResponse), ["application/json"]))
            .AddEndpointFilter<WorldDataEditionFilter>();

        group.MapGetBoss();
        group.MapGetBosses();
        group.MapGetBossSpells();
        group.MapGetClasses();
        group.MapGetClassSpecs();
        group.MapGetRoles();
        group.MapGetRoleSpecs();
        group.MapGetSeason();
        group.MapGetSeasons();
        group.MapGetSpec();
        group.MapGetSpecs();
        group.MapGetSpecSpells();
        group.MapGetSpell();
        group.MapGetSpells();
        group.MapGetTrinket();
        group.MapGetTrinkets();
        group.MapGetZone();
        group.MapGetZoneBosses();
        group.MapGetZones();

        return group;
    }
}
