using System.Text.Json;
using Lorrgs.WarcraftLogs.Contracts;

namespace Lorrgs.WarcraftLogs.Tests;

public sealed class WclCharacterRankingsParserTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParseAcceptsObjectAndJsonEncodedEnvelopes(bool encoded)
    {
        const string json = """
            {"page":2,"count":101,"hasMorePages":true,"error":"partial",
             "rankings":[{"name":"Healer","percentile":99.5,"amount":123.4,"duration":60000,
             "hidden":true,"kill":true,"startTime":123456789,
             "report":{"code":"abc","fightID":7,"title":"Raid","startTime":123},
             "allCharacters":[{"name":"Tank","class":"Warrior","spec":"Protection","guild":"Guild"}]}]}
            """;
        using var document = JsonDocument.Parse(encoded ? JsonSerializer.Serialize(json) : json);

        var result = WclCharacterRankingsParser.Parse(document.RootElement);

        Assert.NotNull(result);
        Assert.Equal(2, result.Page);
        Assert.Equal(101, result.Count);
        Assert.True(result.HasMorePages);
        Assert.Equal("partial", result.Error);
        var entry = Assert.Single(result.Rankings);
        Assert.Equal("Healer", entry.Name);
        Assert.Equal(99.5, entry.Percentile);
        Assert.Equal(123.4, entry.Amount);
        Assert.Equal(60000, entry.Duration);
        Assert.True(entry.Hidden);
        Assert.True(entry.Kill);
        Assert.Equal(123456789, entry.StartTime);
        Assert.Equal(new WclRankingReportNode("abc", "Raid", 7, 123), entry.Report);
        Assert.Equal("Tank", Assert.Single(entry.AllCharacters!).Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParseAcceptsBareRankingArrays(bool encoded)
    {
        const string json = """[{"name":"Player"}]""";
        using var document = JsonDocument.Parse(encoded ? JsonSerializer.Serialize(json) : json);

        var result = WclCharacterRankingsParser.Parse(document.RootElement);

        Assert.NotNull(result);
        Assert.Equal("Player", Assert.Single(result.Rankings).Name);
        Assert.Equal(0, result.Page);
        Assert.Equal(0, result.Count);
        Assert.False(result.HasMorePages);
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("42")]
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    [InlineData("\"invalid json\"")]
    public void ParseReturnsNullForAbsentOrUnsupportedPayloads(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Null(WclCharacterRankingsParser.Parse(document.RootElement));
    }

    [Fact]
    public void ParseReturnsNullForUndefinedPayload() =>
        Assert.Null(WclCharacterRankingsParser.Parse(default));

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"rankings\":null}")]
    [InlineData("\"{}\"")]
    public void MissingRankingsBecomeEmptyCollections(string json)
    {
        using var document = JsonDocument.Parse(json);
        var result = WclCharacterRankingsParser.Parse(document.RootElement);
        Assert.NotNull(result);
        Assert.Empty(result.Rankings);
    }

    [Theory]
    [InlineData("{\"region\":\"EU\",\"server\":\"Realm\",\"faction\":\"Alliance\"}", "EU", "Realm", "Alliance")]
    [InlineData("{\"server\":{\"region\":\"US\",\"name\":\"Realm\"}}", "US", "Realm", null)]
    [InlineData("{\"region\":123,\"server\":true,\"faction\":false}", "123", "True", "False")]
    [InlineData("{\"server\":{\"region\":false,\"name\":123}}", "False", "123", null)]
    [InlineData("{\"server\":{\"region\":true,\"name\":false}}", "True", "False", null)]
    [InlineData("{\"server\":{}}", null, null, null)]
    [InlineData("{\"region\":null,\"server\":{\"region\":[],\"name\":{}},\"faction\":[]}", null, null, null)]
    [InlineData("{}", null, null, null)]
    public void ExtensionAliasesSupportScalarAndNestedUpstreamShapes(
        string json, string? region, string? server, string? faction)
    {
        var entry = JsonSerializer.Deserialize<WclCharacterRankingEntry>(json)!;
        var character = JsonSerializer.Deserialize<WclRankingCharacterNode>(json)!;

        Assert.Equal(region, entry.RegionAlias);
        Assert.Equal(server, entry.ServerAlias);
        Assert.Equal(faction, entry.FactionAlias);
        Assert.Equal(region, character.RegionAlias);
        Assert.Equal(server, character.ServerAlias);
    }
}
