using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lorrgs.Api.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Lorrgs.Api.Services;

public class RaidCatalogClient(
    ILogger<RaidCatalogClient> logger,
    HttpClient httpClient,
    IMemoryCache cache)
{
    private const string CacheKey = "raid-catalog";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> RaidPhases =
        new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.OrdinalIgnoreCase)
        {
            ["classic"] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["moltencore"] = 1,
                ["mc"] = 1,
                ["onyxia"] = 1,
                ["blackwinglair"] = 3,
                ["bwl"] = 3,
                ["azuregos"] = 2,
                ["lordkazzak"] = 2,
                ["dragonsofnightmare"] = 4,
                ["zulgurub"] = 4,
                ["zg"] = 4,
                ["aq20"] = 5,
                ["aq40"] = 5,
                ["naxxramas"] = 6
            },
            ["classic_sod"] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["blackfathomdeeps"] = 1,
                ["bfdsod"] = 1,
                ["gnomeregan"] = 2,
                ["gnomeregansod"] = 2,
                ["sunkentemplesod"] = 3,
                ["mcsod"] = 4,
                ["onyxiasod"] = 4,
                ["bwlsod"] = 5,
                ["zgsod"] = 5,
                ["aq20sod"] = 6,
                ["aq40sod"] = 6,
                ["naxxramassod"] = 7,
                ["scarletenclavesod"] = 7,
                ["crystalvale"] = 7,
                ["azuregossod"] = 4,
                ["lordkazzaksod"] = 4,
                ["dragonsofnightmaresod"] = 4
            },
            ["tbc"] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["karazhan"] = 1,
                ["kara"] = 1,
                ["gruulslair"] = 1,
                ["gruul"] = 1,
                ["magtheridonslair"] = 1,
                ["magtheridon"] = 1,
                ["serpentshrine"] = 2,
                ["ssc"] = 2,
                ["theeye"] = 2,
                ["tempestkeep"] = 2,
                ["hyjalsummit"] = 3,
                ["hyjal"] = 3,
                ["blacktemple"] = 3,
                ["zulaman"] = 4,
                ["za"] = 4,
                ["sunwellplateau"] = 5,
                ["doomlordkazzak"] = 1,
                ["doomwalker"] = 1
            },
            ["wotlk"] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["naxxramas"] = 1,
                ["wotlknaxx10"] = 1,
                ["wotlknaxx25"] = 1,
                ["obsidiansanctum"] = 1,
                ["obsidiansanctum10"] = 1,
                ["obsidiansanctum25"] = 1,
                ["eyeofeternity"] = 1,
                ["eyeofeternity10"] = 1,
                ["eyeofeternity25"] = 1,
                ["vaultofarchavon"] = 1,
                ["ulduar"] = 2,
                ["ulduar10"] = 2,
                ["ulduar25"] = 2,
                ["trialofthecrusader"] = 3,
                ["toc10"] = 3,
                ["toc25"] = 3,
                ["onyxiaslair"] = 3,
                ["wotlkonyxia10"] = 3,
                ["wotlkonyxia25"] = 3,
                ["icecrowncitadel"] = 4,
                ["icc10"] = 4,
                ["icc25"] = 4,
                ["rubesanctum"] = 4,
                ["rubysanctum"] = 5,
                ["rubysanctum10"] = 5,
                ["rubysanctum25"] = 5
            },
            ["cata"] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["baradinhold"] = 1,
                ["blackwingdescent"] = 1,
                ["bastionoftwilight"] = 1,
                ["throneofthefourwinds"] = 1,
                ["throneoffourwinds"] = 1,
                ["firelands"] = 3,
                ["dragonsoul"] = 4
            },
            ["mop"] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["mogushanvaults"] = 1,
                ["heartoffear"] = 1,
                ["terraceofendspring"] = 1,
                ["terraceofendlessspring"] = 1,
                ["throneofthunder"] = 2,
                ["siegeoforgrimmar"] = 3
            }
        };

    private static readonly Lazy<Dictionary<string, string>> BossSlugByName = new(() =>
        WorldDataService.Instance.Bosses
            .Where(b => !string.IsNullOrWhiteSpace(b.Name) && !string.IsNullOrWhiteSpace(b.NameSlug))
            .GroupBy(b => NormalizeLookupKey(b.Name))
            .ToDictionary(g => g.Key, g => g.First().NameSlug, StringComparer.OrdinalIgnoreCase));

    private readonly ILogger<RaidCatalogClient> _logger = logger;
    private readonly HttpClient _httpClient = httpClient;
    private readonly IMemoryCache _cache = cache;

    public async Task<RaidCatalog> GetCatalogAsync()
    {
        if (_cache.TryGetValue(CacheKey, out RaidCatalog? cached) && cached != null)
        {
            return cached;
        }

        _logger.LogInformation("Fetching raid catalog reference data");

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://softres.it/");
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        var catalog = ParseCatalog(html);

        _cache.Set(CacheKey, catalog, CacheDuration);
        return catalog;
    }

    private static RaidCatalog ParseCatalog(string html)
    {
        var match = Regex.Match(html, "data-page=\"(?<payload>[^\"]+)\"", RegexOptions.Singleline);
        if (!match.Success)
        {
            throw new InvalidOperationException("Unable to locate reference data payload.");
        }

        var json = WebUtility.HtmlDecode(match.Groups["payload"].Value);
        using var document = JsonDocument.Parse(json);

        var props = document.RootElement.GetProperty("props");
        var catalog = new RaidCatalog
        {
            Editions = ParseEditions(props),
            Instances = ParseInstances(props)
        };

        return catalog;
    }

    private static List<RaidEdition> ParseEditions(JsonElement props)
    {
        var editions = new List<RaidEdition>();

        if (!props.TryGetProperty("editions", out var editionsElement) || editionsElement.ValueKind != JsonValueKind.Array)
        {
            return editions;
        }

        foreach (var editionElement in editionsElement.EnumerateArray())
        {
            editions.Add(new RaidEdition
            {
                Id = GetInt(editionElement, "id"),
                Slug = GetString(editionElement, "slug"),
                Name = GetString(editionElement, "name"),
                Order = GetInt(editionElement, "order"),
                Public = GetBool(editionElement, "public")
            });
        }

        return editions;
    }

    private static Dictionary<string, List<RaidInstance>> ParseInstances(JsonElement props)
    {
        var instances = new Dictionary<string, List<RaidInstance>>(StringComparer.OrdinalIgnoreCase);

        if (!props.TryGetProperty("instances", out var instancesElement) || instancesElement.ValueKind != JsonValueKind.Object)
        {
            return instances;
        }

        foreach (var editionProperty in instancesElement.EnumerateObject())
        {
            var raids = new List<RaidInstance>();

            if (editionProperty.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var raidElement in editionProperty.Value.EnumerateArray())
                {
                    raids.Add(new RaidInstance
                    {
                        Id = GetInt(raidElement, "id"),
                        Slug = GetString(raidElement, "slug"),
                        Name = GetString(raidElement, "name"),
                        Edition = editionProperty.Name,
                        Phase = GetPhase(editionProperty.Name, GetString(raidElement, "slug")),
                        Slots = GetInt(raidElement, "slots"),
                        NormalAndHeroic = GetBool(raidElement, "normal_and_heroic"),
                        BossOrder = GetStringArray(raidElement, "boss_order"),
                        Bosses = MapBosses(GetStringArray(raidElement, "boss_order")),
                        Public = GetBool(raidElement, "public")
                    });
                }
            }

            instances[editionProperty.Name] = raids;
        }

        return instances;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static int GetInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value)
            ? value
            : 0;
    }

    private static bool GetBool(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.True;
    }

    private static string[] GetStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }

    private static int GetPhase(string editionSlug, string raidSlug)
    {
        if (RaidPhases.TryGetValue(editionSlug, out var editionPhases) && editionPhases.TryGetValue(raidSlug, out var phase))
        {
            return phase;
        }

        return 0;
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