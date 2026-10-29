namespace Lorrgs.WarcraftLogs.Rankings;

internal sealed record CharacterLookupRequest(
    string Name, string ServerSlug, string ServerRegion, string Key);
