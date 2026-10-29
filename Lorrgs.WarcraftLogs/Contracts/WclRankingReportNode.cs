using System.Text.Json.Serialization;

namespace Lorrgs.WarcraftLogs.Contracts;

public sealed record WclRankingReportNode(
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("fightID")] int FightId,
    [property: JsonPropertyName("startTime")] long StartTime);
