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
    [JsonConverter(typeof(WclGuildConverter))]
    public WclGuildNode? Guild { get; init; }

    [JsonPropertyName("serverRegion")]
    public string? ServerRegion { get; init; }

    [JsonPropertyName("regionName")]
    public string? RegionName { get; init; }

    [JsonPropertyName("serverSlug")]
    public string? ServerSlug { get; init; }

    [JsonPropertyName("serverName")]
    public string? ServerName { get; init; }

    [JsonPropertyName("realmName")]
    public string? RealmName { get; init; }

    [JsonPropertyName("allCharacters")]
    public List<WclRankingCharacterNode>? AllCharacters { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }

    [JsonIgnore]
    public string? RegionAlias =>
        WclRankingJsonHelpers.TryGetExtensionString(ExtensionData, "region")
        ?? WclRankingJsonHelpers.TryGetNestedExtensionString(ExtensionData, "server", "region");

    [JsonIgnore]
    public string? ServerAlias =>
        WclRankingJsonHelpers.TryGetExtensionString(ExtensionData, "server")
        ?? WclRankingJsonHelpers.TryGetNestedExtensionString(ExtensionData, "server", "name");

    [JsonIgnore]
    public string? FactionAlias => WclRankingJsonHelpers.TryGetExtensionString(ExtensionData, "faction");
}

public sealed record WclRankingReportNode(
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("fightID")] int FightId,
    [property: JsonPropertyName("startTime")] long StartTime);

public sealed record WclRankingCharacterNode(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("class")] string? Class,
    [property: JsonPropertyName("spec")] string? Spec,
    [property: JsonPropertyName("serverRegion")] string? ServerRegion,
    [property: JsonPropertyName("regionName")] string? RegionName,
    [property: JsonPropertyName("serverSlug")] string? ServerSlug,
    [property: JsonPropertyName("serverName")] string? ServerName,
    [property: JsonPropertyName("realmName")] string? RealmName,
    [property: JsonPropertyName("guild")] string? GuildName)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }

    [JsonIgnore]
    public string? RegionAlias =>
        WclRankingJsonHelpers.TryGetExtensionString(ExtensionData, "region")
        ?? WclRankingJsonHelpers.TryGetNestedExtensionString(ExtensionData, "server", "region");

    [JsonIgnore]
    public string? ServerAlias =>
        WclRankingJsonHelpers.TryGetExtensionString(ExtensionData, "server")
        ?? WclRankingJsonHelpers.TryGetNestedExtensionString(ExtensionData, "server", "name");
}

public sealed record WclGuildNode(
    string? Name,
    int? Faction);

public sealed class WclGuildConverter : JsonConverter<WclGuildNode?>
{
    public override WclGuildNode? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new WclGuildNode(reader.GetString(), null);
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;

            string? name = null;
            int? faction = null;

            if (root.TryGetProperty("name", out var nameProperty) &&
                nameProperty.ValueKind == JsonValueKind.String)
            {
                name = nameProperty.GetString();
            }

            faction = TryReadInt32(root, "faction")
                ?? TryReadInt32(root, "factionID")
                ?? TryReadInt32(root, "factionId");

            return string.IsNullOrWhiteSpace(name) && !faction.HasValue
                ? null
                : new WclGuildNode(name, faction);
        }

        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        _ = JsonDocument.ParseValue(ref reader);
        return null;
    }

    public override void Write(Utf8JsonWriter writer, WclGuildNode? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        if (!string.IsNullOrWhiteSpace(value.Name))
        {
            writer.WriteString("name", value.Name);
        }

        if (value.Faction.HasValue)
        {
            writer.WriteNumber("faction", value.Faction.Value);
        }

        writer.WriteEndObject();
    }

    private static int? TryReadInt32(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return TryReadInt32(property);
    }

    private static int? TryReadInt32(JsonElement property)
    {
        if (property.ValueKind == JsonValueKind.Number)
        {
            return property.TryGetInt32(out var number) ? number : null;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            return int.TryParse(property.GetString(), out var number) ? number : null;
        }

        return null;
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

internal static class WclRankingJsonHelpers
{
    public static string? TryGetExtensionString(Dictionary<string, JsonElement>? extensionData, string key)
    {
        if (extensionData == null || !extensionData.TryGetValue(key, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => null
        };
    }

    public static string? TryGetNestedExtensionString(
        Dictionary<string, JsonElement>? extensionData,
        string key,
        string nestedProperty)
    {
        if (extensionData == null || !extensionData.TryGetValue(key, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!value.TryGetProperty(nestedProperty, out var nestedValue))
        {
            return null;
        }

        return nestedValue.ValueKind switch
        {
            JsonValueKind.String => nestedValue.GetString(),
            JsonValueKind.Number => nestedValue.ToString(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => null
        };
    }
}