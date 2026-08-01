using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lorrgs.WarcraftLogs.Contracts;

public sealed record WclSpecRankingsResponse(
    [property: JsonPropertyName("worldData")] WclWorldDataNode? WorldData);

public sealed record WclWorldDataNode(
    [property: JsonPropertyName("zone")] WclZoneNode? Zone);

public sealed record WclZoneNode(
    [property: JsonPropertyName("encounters")] List<WclEncounterNode>? Encounters);

public sealed record WclEncounterNode(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("characterRankings")] JsonElement CharacterRankings);

public sealed record WclCharacterRankingsEnvelope(
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("hasMorePages")] bool HasMorePages,
    [property: JsonPropertyName("rankings")] List<WclCharacterRankingEntry>? Rankings,
    [property: JsonPropertyName("error")] string? Error);

public sealed record WclCharacterRankingsPayload(
    int Page,
    int Count,
    bool HasMorePages,
    List<WclCharacterRankingEntry> Rankings,
    string? Error);

public sealed record WclCharacterRankingEntry
{
    [JsonPropertyName("hidden")]
    public bool Hidden { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("percentile")]
    public double? Percentile { get; init; }

    [JsonPropertyName("duration")]
    public int Duration { get; init; }

    [JsonPropertyName("amount")]
    public double Amount { get; init; }

    [JsonPropertyName("startTime")]
    public long StartTime { get; init; }

    [JsonPropertyName("kill")]
    public bool? Kill { get; init; }

    [JsonPropertyName("report")]
    public WclRankingReportNode? Report { get; init; }

    [JsonPropertyName("guild")]
    [JsonConverter(typeof(WclGuildNameConverter))]
    public string? GuildName { get; init; }

    [JsonPropertyName("allCharacters")]
    public List<WclRankingCharacterNode>? AllCharacters { get; init; }
}

public sealed record WclRankingReportNode(
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("fightID")] int FightId,
    [property: JsonPropertyName("startTime")] long StartTime);

public sealed record WclRankingCharacterNode(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("class")] string? Class,
    [property: JsonPropertyName("spec")] string? Spec);

public sealed class WclGuildNameConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            if (document.RootElement.TryGetProperty("name", out var nameProperty) &&
                nameProperty.ValueKind == JsonValueKind.String)
            {
                return nameProperty.GetString();
            }

            return null;
        }

        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        using var fallback = JsonDocument.ParseValue(ref reader);
        return fallback.RootElement.ToString();
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value);
    }
}

public static class WclCharacterRankingsParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static WclCharacterRankingsPayload? Parse(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Undefined || payload.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (payload.ValueKind == JsonValueKind.String)
        {
            var raw = payload.GetString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            try
            {
                raw = raw.Trim();
                if (raw.StartsWith("[", StringComparison.Ordinal))
                {
                    var rankings = JsonSerializer.Deserialize<List<WclCharacterRankingEntry>>(raw, JsonOptions) ?? [];
                    return new WclCharacterRankingsPayload(0, 0, false, rankings, null);
                }

                var envelope = JsonSerializer.Deserialize<WclCharacterRankingsEnvelope>(raw, JsonOptions);
                return new WclCharacterRankingsPayload(
                    envelope?.Page ?? 0,
                    envelope?.Count ?? 0,
                    envelope?.HasMorePages ?? false,
                    envelope?.Rankings ?? [],
                    envelope?.Error);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        if (payload.ValueKind == JsonValueKind.Array)
        {
            var rankings = JsonSerializer.Deserialize<List<WclCharacterRankingEntry>>(payload.GetRawText(), JsonOptions) ?? [];
            return new WclCharacterRankingsPayload(0, 0, false, rankings, null);
        }

        if (payload.ValueKind == JsonValueKind.Object)
        {
            var envelope = JsonSerializer.Deserialize<WclCharacterRankingsEnvelope>(payload.GetRawText(), JsonOptions);
            return new WclCharacterRankingsPayload(
                envelope?.Page ?? 0,
                envelope?.Count ?? 0,
                envelope?.HasMorePages ?? false,
                envelope?.Rankings ?? [],
                envelope?.Error);
        }

        return null;
    }
}