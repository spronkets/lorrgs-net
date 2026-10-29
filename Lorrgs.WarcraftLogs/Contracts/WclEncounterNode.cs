using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lorrgs.WarcraftLogs.Contracts;

public sealed record WclEncounterNode(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("characterRankings")] JsonElement CharacterRankings);
