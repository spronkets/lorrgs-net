namespace Lorrgs.WarcraftLogs.Services;

/// <summary>Validated fight ranking search options; kill times are seconds.</summary>
public sealed record CompRankingSearch(string BossSlug, string Edition, int EncounterId, int Difficulty,
    int Page, int MinSeconds, int MaxSeconds, IReadOnlyList<CompCountFilter> Roles,
    IReadOnlyList<CompCountFilter> Specs, bool Refresh);
