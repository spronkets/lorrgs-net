using Lorrgs.WarcraftLogs.Services;

namespace Lorrgs.WarcraftLogs.Tests;

public sealed class GraphQlResourcesTests
{
    [Fact]
    public void EveryGraphQlDocumentIsEmbeddedInTheLibraryAndCanBeLoaded()
    {
        const string prefix = "Lorrgs.WarcraftLogs.GraphQL.";
        var resources = typeof(WarcraftLogsClient).Assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(".graphql", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(resources);
        foreach (var resource in resources)
        {
            Assert.StartsWith(prefix, resource);
            var name = resource[prefix.Length..];
            var document = GraphQlResourceLoader.Load(name);
            Assert.Contains("query", document);
            Assert.Same(document, GraphQlResourceLoader.Load(name));
            Assert.Equal(document, GraphQlResourceLoader.Load(name[..^8].Replace('.', '\\') + ".graphql"));
            Assert.Equal(document, GraphQlResourceLoader.Load(name[..^8].Replace('.', '/') + ".graphql"));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void EmptyResourceNamesAreRejected(string name) =>
        Assert.Throws<ArgumentException>(() => GraphQlResourceLoader.Load(name));

    [Fact]
    public void MissingResourcesFailExplicitly() =>
        Assert.Contains("Missing.graphql", Assert.Throws<InvalidOperationException>(() =>
            GraphQlResourceLoader.Load("Missing.graphql")).Message);

    [Fact]
    public void LibraryDoesNotReferenceTheApiOrAspNetCore()
    {
        var references = typeof(WarcraftLogsClient).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(references, reference => reference.Name == "Lorrgs.Api" ||
            reference.Name!.StartsWith("Microsoft.AspNetCore.", StringComparison.Ordinal));
    }
}
