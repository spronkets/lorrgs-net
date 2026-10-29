using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lorrgs.WarcraftLogs.Contracts;

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
