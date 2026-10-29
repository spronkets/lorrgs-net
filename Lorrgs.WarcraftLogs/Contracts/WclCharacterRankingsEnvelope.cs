using System.Text.Json.Serialization;

namespace Lorrgs.WarcraftLogs.Contracts;

public sealed record WclCharacterRankingsEnvelope(
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("hasMorePages")] bool HasMorePages,
    [property: JsonPropertyName("rankings")] List<WclCharacterRankingEntry>? Rankings,
    [property: JsonPropertyName("error")] string? Error);
