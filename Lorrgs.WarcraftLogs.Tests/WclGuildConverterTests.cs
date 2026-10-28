using System.Text.Json;
using Lorrgs.WarcraftLogs.Contracts;

namespace Lorrgs.WarcraftLogs.Tests;

public sealed class WclGuildConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new WclGuildConverter() }
    };

    [Theory]
    [InlineData("\"Guild\"", "Guild", null)]
    [InlineData("{\"name\":\"Guild\",\"faction\":1}", "Guild", 1)]
    [InlineData("{\"name\":\"Guild\",\"factionID\":\"2\"}", "Guild", 2)]
    [InlineData("{\"factionId\":1}", null, 1)]
    [InlineData("{\"name\":7,\"faction\":1.5,\"factionID\":true,\"factionId\":\"bad\"}", null, null)]
    [InlineData("{}", null, null)]
    [InlineData("{\"name\":\" \"}", null, null)]
    [InlineData("null", null, null)]
    [InlineData("42", null, null)]
    [InlineData("[]", null, null)]
    public void ReadHandlesGuildVariantsWithoutInventingData(string json, string? name, int? faction)
    {
        var guild = JsonSerializer.Deserialize<WclGuildNode>(json, Options);
        Assert.Equal(name is null && faction is null ? null : new WclGuildNode(name, faction), guild);
    }

    [Theory]
    [InlineData("Guild", 1, "{\"name\":\"Guild\",\"faction\":1}")]
    [InlineData("Guild", null, "{\"name\":\"Guild\"}")]
    [InlineData(null, 2, "{\"faction\":2}")]
    [InlineData("", null, "{}")]
    public void WriteOmitsAbsentFields(string? name, int? faction, string expected) =>
        Assert.Equal(expected, JsonSerializer.Serialize(new WclGuildNode(name, faction), Options));

    [Fact]
    public void WriteNullGuildProducesJsonNull() =>
        Assert.Equal("null", JsonSerializer.Serialize<WclGuildNode?>(null, Options));
}
