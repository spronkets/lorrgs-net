import type { RankingReport } from './models/rankings/RankingReport';
import type { RankingFilters } from './models/rankings/RankingFilters';
import type { RankingSort } from './models/rankings/RankingSort';
import type { RankingPlayer } from './models/RankingPlayer';

function warcraftLogsHost(edition: string): string {
  return { anniversary: 'fresh', mistsofpandaria: 'classic', era: 'vanilla' }[edition] ?? 'www';
}

export function getPlayerProfileLink(
  player: Pick<RankingPlayer, 'name' | 'serverRegion' | 'serverName' | 'serverSlug'> | undefined,
  edition: string
): string | null {
  const region = normalizeRankingRegion(player?.serverRegion);
  const server = (player?.serverSlug || player?.serverName || '').trim().toLowerCase().replace(/\s+/g, '-');
  const name = player?.name.trim().toLowerCase();
  if (!region || !server || !name) return null;
  return `https://${warcraftLogsHost(edition)}.warcraftlogs.com/character/${encodeURIComponent(region)}/${encodeURIComponent(server)}/${encodeURIComponent(name)}`;
}

export function parseKillTime(value: string): number | null {
  const trimmed = value.trim();
  if (!trimmed) return null;
  if (!/^\d+(?::[0-5]\d)?$/.test(trimmed)) return NaN;
  const parts = trimmed.split(':').map(Number);
  const seconds = parts.length === 2 ? parts[0] * 60 + parts[1] : parts[0];
  return Number.isSafeInteger(seconds) ? seconds * 1000 : NaN;
}

export function toKillTimeInput(value: string): string {
  const milliseconds = parseKillTime(value);
  if (milliseconds === null || !Number.isFinite(milliseconds)) return value;
  const seconds = milliseconds / 1000;
  return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`;
}

export function getRankingFilterError(filters: RankingFilters): string {
  const min = parseKillTime(filters.minDuration);
  const max = parseKillTime(filters.maxDuration);
  if (Number.isNaN(min) || Number.isNaN(max)) {
    return 'Enter kill times as m:ss (for example, 5:32).';
  }
  if (min !== null && max !== null && min > max) {
    return 'Minimum kill time must not exceed maximum kill time.';
  }
  if (filters.fromDate && filters.toDate && filters.fromDate > filters.toDate) {
    return 'From date must not be after To date.';
  }
  return '';
}

export function normalizeRankingRegion(region: string | null | undefined): string {
  const normalized = (region ?? '').trim().toLowerCase();
  return normalized === 'na' ? 'us' : normalized;
}

export function selectRankingReports<T extends RankingReport>(
  reports: T[],
  filters: RankingFilters,
  sort: RankingSort
): { report: T; rank: number }[] {
  if (getRankingFilterError(filters)) return [];
  const min = parseKillTime(filters.minDuration);
  const max = parseKillTime(filters.maxDuration);
  const from = filters.fromDate ? new Date(`${filters.fromDate}T00:00:00`).getTime() : null;
  const to = filters.toDate ? new Date(`${filters.toDate}T00:00:00`) : null;
  if (to) to.setDate(to.getDate() + 1);
  return reports
    .map((report, index) => ({ report, rank: index + 1 }))
    .filter(({ report }) => {
      if (
        filters.region &&
        normalizeRankingRegion(report.players?.[0]?.serverRegion) !== filters.region
      )
        return false;
      if (min !== null || max !== null) {
        if (typeof report.duration !== 'number' || !Number.isFinite(report.duration)) return false;
        if (min !== null && report.duration < min) return false;
        if (max !== null && report.duration > max) return false;
      }
      if (from !== null || to !== null) {
        const date = report.startTime ? new Date(report.startTime).getTime() : NaN;
        if (!Number.isFinite(date)) return false;
        if (from !== null && date < from) return false;
        if (to !== null && date >= to.getTime()) return false;
      }
      return true;
    })
    .sort((a, b) => {
      const value = (report: T): number | null => {
        const raw =
          sort === 'performance'
            ? report.players?.[0]?.performance
            : sort === 'duration'
              ? report.duration
              : sort === 'date'
                ? new Date(report.startTime ?? '').getTime()
                : null;
        return typeof raw === 'number' && Number.isFinite(raw) ? raw : null;
      };
      if (sort === 'rank') return a.rank - b.rank;
      const av = value(a.report);
      const bv = value(b.report);
      if (av === null) return bv === null ? a.rank - b.rank : 1;
      if (bv === null) return -1;
      return (sort === 'duration' ? av - bv : bv - av) || a.rank - b.rank;
    });
}

export function summarizeRankingReports(reports: RankingReport[]) {
  const performances = reports
    .map((report) => report.players?.[0]?.performance)
    .filter(
      (value): value is number => typeof value === 'number' && Number.isFinite(value) && value >= 0
    );
  const durations = reports
    .map((report) => report.duration)
    .filter(
      (value): value is number => typeof value === 'number' && Number.isFinite(value) && value > 0
    )
    .sort((a, b) => a - b);
  const middle = Math.floor(durations.length / 2);
  return {
    count: reports.length,
    bestPerformance: performances.length ? Math.max(...performances) : null,
    medianDuration: durations.length
      ? durations.length % 2
        ? durations[middle]
        : (durations[middle - 1] + durations[middle]) / 2
      : null
  };
}

export function formatRankingDuration(milliseconds: number | null | undefined): string {
  if (typeof milliseconds !== 'number' || !Number.isFinite(milliseconds) || milliseconds <= 0)
    return 'N/A';
  const seconds = Math.floor(milliseconds / 1000);
  return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`;
}

export function getRankingReportLink(
  report: { reportId: string; fights?: { fightId: number }[]; players?: { playerId: number }[] },
  edition: string
): string {
  const host = warcraftLogsHost(edition);
  const params = new URLSearchParams();
  const fight = report.fights?.[0]?.fightId;
  const source = report.players?.[0]?.playerId;
  if (fight && fight > 0) params.set('fight', String(fight));
  if (source && source > 0) params.set('source', String(source));
  return `https://${host}.warcraftlogs.com/reports/${encodeURIComponent(report.reportId)}${params.size ? `#${params}` : ''}`;
}

export function getRotationAnalyzerLink(report: {
  reportId: string;
  fights?: { fightId: number }[];
  players?: { playerId: number; name: string }[];
}): string | null {
  if (!report.reportId.trim()) return null;
  const params = new URLSearchParams({ page: 'rotation', reportCode: report.reportId.trim() });
  const fightId = report.fights?.[0]?.fightId;
  const player = report.players?.[0];
  if (fightId && fightId > 0) params.set('fightId', String(fightId));
  if (player?.playerId && player.playerId > 0) params.set('sourceId', String(player.playerId));
  if (player?.name) params.set('playerName', player.name);
  params.set('autoAnalyze', '1');
  return `?${params}`;
}

export function getDisplayPercentile(value: unknown): string {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return `${value.toFixed(1)}%`;
  }

  return 'N/A';
}

export function getTopPercentLabel(value: unknown): string {
  if (typeof value !== 'number' || !Number.isFinite(value) || value < 0 || value > 100)
    return 'N/A';
  const top = 100 - value;
  return `Top ${Math.max(1, Math.ceil(top))}%`;
}
