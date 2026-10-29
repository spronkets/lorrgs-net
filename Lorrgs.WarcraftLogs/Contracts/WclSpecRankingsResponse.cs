using System.Text.Json.Serialization;

namespace Lorrgs.WarcraftLogs.Contracts;

public sealed record WclSpecRankingsResponse(
    [property: JsonPropertyName("worldData")] WclWorldDataNode? WorldData);
