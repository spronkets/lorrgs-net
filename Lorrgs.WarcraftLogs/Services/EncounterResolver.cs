using Lorrgs.WarcraftLogs.Contracts;

namespace Lorrgs.WarcraftLogs.Services;

internal static class EncounterResolver
{
    private static readonly string Query = GraphQlResourceLoader.Load("Rankings.EncounterList.graphql");

    public static async Task<int?> ResolveAsync(WarcraftLogsClient client, int zoneId, string bossSlug,
        string? edition, bool refresh, CancellationToken cancellationToken)
    {
        var response = await client.QueryGraphQLAsync<WclSpecRankingsResponse>(
            Query, new { zoneId }, edition, refresh, cancellationToken);
        var target = NormalizeSlug(bossSlug);
        if (target.Length == 0) return null;
        return response.WorldData?.Zone?.Encounters?
            .FirstOrDefault(encounter => NormalizeSlug(Slugify(encounter.Name)) == target && encounter.Id > 0)?.Id;
    }

    private static string Slugify(string? value)
    {
        var raw = (value ?? string.Empty).Replace("'", string.Empty).Replace("\u2019", string.Empty);
        var chars = raw.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        return slug.Trim('-');
    }

    private static string NormalizeSlug(string? value)
    {
        var slug = (value ?? string.Empty).Trim().ToLowerInvariant().Trim('-');
        var lastDashIndex = slug.LastIndexOf('-');
        return lastDashIndex > 0 && int.TryParse(slug[(lastDashIndex + 1)..], out _)
            ? slug[..lastDashIndex]
            : slug;
    }
}
