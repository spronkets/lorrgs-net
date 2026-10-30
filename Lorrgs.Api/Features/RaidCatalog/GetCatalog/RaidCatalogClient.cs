using Lorrgs.Api.Features.WorldData;
using Lorrgs.Api.Features.WorldData.Models;
using Microsoft.Extensions.Options;

namespace Lorrgs.Api.Features.RaidCatalog.GetCatalog;

public class RaidCatalogClient(
    ILogger<RaidCatalogClient> logger,
    IOptions<RaidCatalogOptions> raidCatalogOptions)
{
    private static readonly Lazy<Dictionary<string, RaidBoss>> BossByName = new(() =>
        WorldDataService.Instance.Bosses
            .Where(b => !string.IsNullOrWhiteSpace(b.Name) && !string.IsNullOrWhiteSpace(b.NameSlug))
            .GroupBy(b => NormalizeLookupKey(b.Name))
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase));

    private readonly ILogger<RaidCatalogClient> _logger = logger;
    private readonly RaidCatalogOptions _options = raidCatalogOptions.Value;

    public Task<RaidCatalog> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation("Building raid catalog from local world data");

        var catalog = BuildCatalog();

        return Task.FromResult(catalog);
    }

    private RaidCatalog BuildCatalog()
    {
        var editions = _options.Editions
            .Select(edition => new RaidEdition
            {
                Slug = edition.Slug,
                Name = edition.Name,
                Order = edition.Order,
                ActivePhase = edition.ActivePhase
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
                    ZoneId = raid.ZoneId,
                    RaidSize = raid.RaidSize,
                    Slug = raid.Slug,
                    Name = raid.Name,
                    Edition = editionSlug,
                    Phase = raid.Phase,
                    Bosses = MapBosses(bossNames),
                    IsAvailable = raid.Phase <= edition.ActivePhase
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
        var bossByName = BossByName.Value;
        var results = new List<RaidBossOption>();

        foreach (var bossName in bossNames)
        {
            var key = NormalizeLookupKey(bossName);
            var mapped = bossByName.TryGetValue(key, out var boss) && !string.IsNullOrWhiteSpace(boss?.NameSlug);

            results.Add(new RaidBossOption
            {
                Id = mapped ? boss!.Id : 0,
                Name = bossName,
                Slug = mapped ? boss!.NameSlug : string.Empty,
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