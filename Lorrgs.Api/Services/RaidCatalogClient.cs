using Lorrgs.Api.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Lorrgs.Api.Services;

public class RaidCatalogClient(
    ILogger<RaidCatalogClient> logger,
    IMemoryCache cache,
    IOptions<RaidCatalogOptions> raidCatalogOptions)
{
    private const string CacheKey = "raid-catalog";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);

    private static readonly Lazy<Dictionary<string, string>> BossSlugByName = new(() =>
        WorldDataService.Instance.Bosses
            .Where(b => !string.IsNullOrWhiteSpace(b.Name) && !string.IsNullOrWhiteSpace(b.NameSlug))
            .GroupBy(b => NormalizeLookupKey(b.Name))
            .ToDictionary(g => g.Key, g => g.First().NameSlug, StringComparer.OrdinalIgnoreCase));

    private readonly ILogger<RaidCatalogClient> _logger = logger;
    private readonly IMemoryCache _cache = cache;
    private readonly RaidCatalogOptions _options = raidCatalogOptions.Value;

    public Task<RaidCatalog> GetCatalogAsync()
    {
        if (_cache.TryGetValue(CacheKey, out RaidCatalog? cached) && cached != null)
        {
            return Task.FromResult(cached);
        }

        _logger.LogInformation("Building raid catalog from local world data");

        var catalog = BuildCatalog();

        _cache.Set(CacheKey, catalog, CacheDuration);
        return Task.FromResult(catalog);
    }

    private RaidCatalog BuildCatalog()
    {
        var editions = _options.Editions
            .Select(edition => new RaidEdition
            {
                Id = edition.Id,
                Slug = edition.Slug,
                Name = edition.Name,
                Order = edition.Order,
                Public = edition.Public
            })
            .OrderBy(edition => edition.Order)
            .ToList();

        var instances = new Dictionary<string, List<RaidInstance>>(StringComparer.OrdinalIgnoreCase);

        foreach (var edition in editions)
        {
            instances[edition.Slug] = [];
        }

        foreach (var edition in _options.Editions)
        {
            var editionSlug = edition.Slug;
            var configuredRaids = new List<RaidInstance>();

            foreach (var raid in edition.Raids)
            {
                var bossNames = raid.Bosses.Where(name => !string.IsNullOrWhiteSpace(name)).ToArray();

                configuredRaids.Add(new RaidInstance
                {
                    Id = raid.Id,
                    Slug = raid.Slug,
                    Name = raid.Name,
                    Edition = editionSlug,
                    Phase = raid.Phase,
                    Bosses = MapBosses(bossNames),
                    Public = raid.Public
                });
            }

            instances[editionSlug] = configuredRaids;
        }

        return new RaidCatalog
        {
            Editions = editions,
            Instances = instances
        };
    }

    private static List<RaidBossOption> MapBosses(IEnumerable<string> bossNames)
    {
        var slugByName = BossSlugByName.Value;
        var results = new List<RaidBossOption>();

        foreach (var bossName in bossNames)
        {
            var key = NormalizeLookupKey(bossName);
            var mapped = slugByName.TryGetValue(key, out var slug) && !string.IsNullOrWhiteSpace(slug);

            results.Add(new RaidBossOption
            {
                Name = bossName,
                Slug = mapped ? slug! : string.Empty,
                Mapped = mapped
            });
        }

        return results;
    }

    private static string NormalizeLookupKey(string? value)
    {
        var raw = value ?? string.Empty;
        raw = raw.Replace("'", string.Empty).Replace("’", string.Empty);
        var chars = raw
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : ' ')
            .ToArray();
        return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}