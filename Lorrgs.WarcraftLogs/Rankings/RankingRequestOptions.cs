namespace Lorrgs.WarcraftLogs.Rankings;

public readonly record struct RankingRequestOptions(
        int Page,
        int? Partition,
        int? Bracket,
        int? Size,
        string? ServerRegion,
        string? ServerSlug,
        string? Filter,
        string? HardModeLevel,
        string? ExternalBuffs,
        bool IncludeOtherPlayers,
        bool IncludeCombatantInfo);
