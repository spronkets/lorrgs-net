using System.Text.Json.Serialization;

namespace Lorrgs.WarcraftLogs.Contracts;

public sealed record WclWorldDataNode(
    [property: JsonPropertyName("zone")] WclZoneNode? Zone,
    [property: JsonPropertyName("encounter")] WclEncounterNode? Encounter);
