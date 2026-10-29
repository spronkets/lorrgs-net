namespace Lorrgs.WarcraftLogs.Rankings;

public sealed record RankingQueryContext(
        int ZoneId,
        int EncounterId,
        string ClassName,
        string SpecName,
        string SpecSlug,
        string BossSlug,
        string BossName,
        int ClassId,
        int SpecId);
