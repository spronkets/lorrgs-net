namespace Lorrgs.WarcraftLogs.ReferenceData;

public sealed record ReferenceSpec(
    int Id, string Name, string NameSlug, string FullNameSlug, int ClassId, int RoleId);
