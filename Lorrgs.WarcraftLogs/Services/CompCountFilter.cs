using System.Text.RegularExpressions;

namespace Lorrgs.WarcraftLogs.Services;

/// <summary>A validated composition count comparison.</summary>
public sealed record CompCountFilter(string Key, string Operator, int Count)
{
    public static CompCountFilter Parse(string expression)
    {
        var match = Regex.Match(expression, @"\A([a-z0-9-]+)\.(?:(eq|lt|gt|lte|gte)\.)?(\d+)\z",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (!match.Success || !int.TryParse(match.Groups[3].Value, out var count) || count > 100)
            throw new ArgumentException("Count filters must be key.eq|lt|gt|lte|gte.count (0–100).");
        return new(match.Groups[1].Value, match.Groups[2].Success ? match.Groups[2].Value : "eq", count);
    }

    public bool Matches(IReadOnlyDictionary<string, int> counts)
    {
        var value = counts.GetValueOrDefault(Key);
        return Operator switch
        {
            "eq" => value == Count, "lt" => value < Count, "gt" => value > Count,
            "lte" => value <= Count, "gte" => value >= Count, _ => false
        };
    }
}
