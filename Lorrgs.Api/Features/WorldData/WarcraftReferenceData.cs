using Lorrgs.WarcraftLogs.ReferenceData;

namespace Lorrgs.Api.Features.WorldData;

public sealed class WarcraftReferenceData(WorldDataService data) : IWarcraftReferenceData
{
    public IReadOnlyList<ReferenceClass> Classes { get; } =
        data.Classes.Select(item => new ReferenceClass(item.Id, item.Name, item.NameSlug)).ToArray();

    public IReadOnlyList<ReferenceSpec> Specs { get; } =
        data.Specs.Select(item => new ReferenceSpec(
            item.Id, item.Name, item.NameSlug, item.FullNameSlug, item.ClassId, item.RoleId)).ToArray();

    public IReadOnlyList<ReferenceRole> Roles { get; } =
        data.Roles.Select(item => new ReferenceRole(item.Id, item.Code)).ToArray();

    public ReferenceClass? GetClass(int classId) => Classes.FirstOrDefault(item => item.Id == classId);

    public ReferenceSpec? GetSpec(string fullNameSlug) =>
        Specs.FirstOrDefault(item => item.FullNameSlug == fullNameSlug);

    public ReferenceBoss? GetBoss(string nameSlug)
    {
        var boss = data.GetBoss(nameSlug);
        return boss is null ? null : new(boss.Id, boss.Name, boss.NameSlug, boss.RaidId, boss.Icon);
    }
}
