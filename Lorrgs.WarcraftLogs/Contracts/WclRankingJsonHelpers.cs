using System.Text.Json;

namespace Lorrgs.WarcraftLogs.Contracts;

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
