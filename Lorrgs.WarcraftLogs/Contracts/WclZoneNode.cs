using System.Text.Json.Serialization;

namespace Lorrgs.WarcraftLogs.Contracts;

public sealed record WclZoneNode(
    [property: JsonPropertyName("encounters")] List<WclEncounterNode>? Encounters);
