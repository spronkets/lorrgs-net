namespace Lorrgs.WarcraftLogs.ReferenceData;

/// <summary>Static game identities used to enrich Warcraft Logs results.</summary>
public interface IWarcraftReferenceData
{
    IReadOnlyList<ReferenceClass> Classes { get; }
    IReadOnlyList<ReferenceSpec> Specs { get; }
    IReadOnlyList<ReferenceRole> Roles { get; }
    ReferenceClass? GetClass(int classId);
    ReferenceSpec? GetSpec(string fullNameSlug);
    ReferenceBoss? GetBoss(string nameSlug);
}
