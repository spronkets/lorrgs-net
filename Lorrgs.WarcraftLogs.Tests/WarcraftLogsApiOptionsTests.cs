using Lorrgs.WarcraftLogs.Configuration;

namespace Lorrgs.WarcraftLogs.Tests;

public sealed class WarcraftLogsApiOptionsTests
{
    [Theory]
    [InlineData(" Mists of Pandaria ", "mistsofpandaria")]
    [InlineData("ANNIVERSARY", "anniversary")]
    [InlineData("Era", "era")]
    [InlineData("Retail", "retail")]
    [InlineData("custom-edition", "custom-edition")]
    public void ResolveEndpointSupportsEditionAliasesAndConfiguredKeys(string edition, string key)
    {
        var configured = new WarcraftLogsEndpointOptions { BaseUrl = "https://edition.test/api", AuthUrl = "https://edition.test/oauth" };
        var options = new WarcraftLogsApiOptions { EditionEndpoints = { [key] = configured } };
        Assert.Same(configured, options.ResolveEndpoint(edition));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("era")]
    public void ResolveEndpointFallsBackToDefaultForMissingOrIncompleteConfiguration(string? edition)
    {
        var options = new WarcraftLogsApiOptions { EditionEndpoints = { ["era"] = new() { BaseUrl = "https://era.test" } } };
        var result = options.ResolveEndpoint(edition);
        Assert.Equal(options.BaseUrl, result.BaseUrl);
        Assert.Equal(options.AuthUrl, result.AuthUrl);
        Assert.Equal(TimeSpan.FromDays(10), options.GraphQlCacheTtl);
    }

    [Theory]
    [InlineData("", "secret", false)]
    [InlineData("client", " ", false)]
    [InlineData("client", "secret", true)]
    public void CredentialsRequireBothNonBlankValues(string client, string secret, bool expected)
    {
        var options = new WarcraftLogsApiOptions { ClientId = client, ClientSecret = secret };
        Assert.Equal(expected, options.HasClientCredentials);
        Assert.Equal(expected, options.IsConfigured);
    }
}
