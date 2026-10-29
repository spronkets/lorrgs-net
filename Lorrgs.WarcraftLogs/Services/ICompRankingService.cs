using Lorrgs.WarcraftLogs.Models;

namespace Lorrgs.WarcraftLogs.Services;

public interface ICompRankingService
{
    Task<CompRankingData?> LoadAsync(CompRankingSearch search, CancellationToken cancellationToken);
    Task<RankingReport> LoadDetailsAsync(string code, int fightId, string edition, bool refresh,
        CancellationToken cancellationToken);
}
