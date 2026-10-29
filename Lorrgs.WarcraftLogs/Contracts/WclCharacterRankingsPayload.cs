namespace Lorrgs.WarcraftLogs.Contracts;

public sealed record WclCharacterRankingsPayload(
    int Page,
    int Count,
    bool HasMorePages,
    List<WclCharacterRankingEntry> Rankings,
    string? Error);
