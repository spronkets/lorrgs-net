using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lorrgs.WarcraftLogs.Contracts;

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
    public string? FactionAlias => WclRankingJsonHelpers.TryGetExtensionString(ExtensionData, "faction");

    [JsonIgnore]
    public string? RegionAlias =>
        WclRankingJsonHelpers.TryGetExtensionString(ExtensionData, "region")
        ?? WclRankingJsonHelpers.TryGetNestedExtensionString(ExtensionData, "server", "region");

    [JsonIgnore]
    public string? ServerAlias =>
        WclRankingJsonHelpers.TryGetExtensionString(ExtensionData, "server")
        ?? WclRankingJsonHelpers.TryGetNestedExtensionString(ExtensionData, "server", "name");
}
