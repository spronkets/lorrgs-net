using Lorrgs.Tests.TestSupport;

namespace Lorrgs.WarcraftLogs.Tests;

public sealed class WarcraftLogsClientTests : IDisposable
{
    private readonly TestClientFixture fixture = new();
    public void Dispose() => fixture.Dispose();

    [Theory]
    [InlineData("You do not have permission to view this report", false)]
    [InlineData("This report has been archived. Subscribing users can access the report content via the /user API endpoint.", true)]
    public async Task PrivateReportErrorsPreservePartialDataAndAreNotCached(string message, bool archived)
    {
        const string query = """
            query Mixed { reportData {
              r0: report(code: "private") { code } r1: report(code: "healthy") { code }
            } }
            """;
        const string partial = """
            {"data":{"reportData":{"r0":null,"r1":{"code":"healthy"}}},
             "errors":[{"message":"You do not have permission to view this report","path":["reportData","r0"]}]}
            """;
        using var handler = new QueueHandler(
            partial.Replace("You do not have permission to view this report", message),
            """{"data":{"ok":true}}""");
        var client = fixture.CreateClient(handler);

        var failure = await Assert.ThrowsAsync<WarcraftLogsReportPrivateException>(() =>
            client.QueryGraphQLAsync(query));

        Assert.True(failure.IsReportAccessFailure);
        Assert.Equal(archived, failure.IsArchived);
        Assert.Contains("r0", failure.InaccessibleReportAliases);
        var error = Assert.Single(failure.Errors);
        Assert.Equal(["reportData", "r0"], error.Path.Select(item => item.GetString()));
        Assert.NotNull(failure.PartialData);
        Assert.Equal("healthy", failure.PartialData.Value.GetProperty("reportData").GetProperty("r1")
            .GetProperty("code").GetString());
        await client.QueryGraphQLAsync(query);
        Assert.Equal(2, handler.GraphQlRequests);
    }

    [Fact]
    public async Task UnrecognizedGraphQlErrorsRemainVisibleAndAreNotCached()
    {
        const string query = """query Other { reportData { r0: report(code: "any") { code } } }""";
        const string error = """
            {"data":{"reportData":{"r0":null}},
             "errors":[{"message":"Unexpected resolver failure","path":["reportData","r0"]}]}
            """;
        using var handler = new QueueHandler(error, """{"data":{"ok":true}}""");
        var client = fixture.CreateClient(handler);

        var failure = await Assert.ThrowsAsync<WarcraftLogsGraphQlException>(() => client.QueryGraphQLAsync(query));

        Assert.False(failure.IsReportAccessFailure);
        Assert.Equal("Unexpected resolver failure", Assert.Single(failure.Errors).Message);
        await client.QueryGraphQLAsync(query);
        Assert.Equal(2, handler.GraphQlRequests);
    }
}
