namespace Lorrgs.Api.Shared;

public static class WorldDataEditions
{
    public static IReadOnlyList<string> Editions { get; } =
        Array.AsReadOnly(new[] { "anniversary", "era", "retail", "mistsofpandaria", "classic_sod" });

    public static string? NormalizeEdition(string? edition) => edition?.Trim().ToLowerInvariant() switch
    {
        "anniversary" => "anniversary",
        "era" or "classic" => "era",
        "retail" or "midnight" => "retail",
        "mistsofpandaria" or "mists of pandaria" or "mop" => "mistsofpandaria",
        "classic_sod" => "classic_sod",
        _ => null
    };

}