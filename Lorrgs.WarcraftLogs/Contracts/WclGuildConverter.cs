using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lorrgs.WarcraftLogs.Contracts;

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
