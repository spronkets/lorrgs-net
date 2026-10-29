using Lorrgs.WarcraftLogs.Models;

namespace Lorrgs.WarcraftLogs.Services;

public interface IReportTimelineService
{
    Task PopulateRosterTimelinesAsync(RankingReport report, string edition, bool refresh,
        CancellationToken cancellationToken);
    Task<ReportTimeline> LoadAsync(string reportCode, int fightId, int? sourceId,
        string? playerName, string edition, CancellationToken cancellationToken, int? specId = null);
    Task<ReportTimeline?> TryLoadCachedAsync(string reportCode, int fightId, int? sourceId,
        string? playerName, string edition, CancellationToken cancellationToken, int? specId = null);
    Task<BossTimeline?> LoadBossAsync(string reportCode, int fightId, string edition,
        bool cacheOnly, CancellationToken cancellationToken);
    Task PopulateFightPhasesAsync(IReadOnlyList<RankingReport> reports, string edition,
        CancellationToken cancellationToken);
}
