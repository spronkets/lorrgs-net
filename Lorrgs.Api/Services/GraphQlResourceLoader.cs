using System.Collections.Concurrent;
using System.Reflection;

namespace Lorrgs.Api.Services;

public static class GraphQlResourceLoader
{
    private const string ResourceRoot = "Lorrgs.Api.GraphQL.";
    private static readonly ConcurrentDictionary<string, string> Cache = new(StringComparer.Ordinal);

    public static string Load(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            throw new ArgumentException("Resource path cannot be empty.", nameof(resourcePath));
        }

        return Cache.GetOrAdd(resourcePath, static path =>
        {
            var resourceName = ResourceRoot + path.Replace('/', '.').Replace('\\', '.');
            var assembly = Assembly.GetExecutingAssembly();

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"GraphQL resource not found: {resourceName}");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        });
    }
}
