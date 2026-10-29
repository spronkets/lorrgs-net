import { SvelteMap } from 'svelte/reactivity';
import { getCompRankingDetails, type RankingReport } from './api';

export function compKey(report: RankingReport): string {
  return `${report.reportId}:${report.fights?.[0]?.fightId ?? 0}`;
}

export class CompDetailLoader {
  readonly loaded = new SvelteMap<string, RankingReport>();
  readonly errors = new SvelteMap<string, string>();
  loadingKey = $state('');
  stopped = $state(false);
  private readonly controller = new AbortController();

  constructor(
    private readonly edition: string,
    private readonly refresh: boolean
  ) {}

  async start(reports: RankingReport[]) {
    // Exactly the first three kills, not the first three matches. No backfill on failures.
    for (const report of reports.slice(0, 3)) {
      if (this.controller.signal.aborted || this.stopped) break;
      if (!(await this.load(report))) break;
    }
  }

  async load(report: RankingReport): Promise<boolean> {
    if (this.loadingKey || this.controller.signal.aborted) return false;
    const key = compKey(report);
    this.loadingKey = key;
    this.errors.delete(key);
    this.stopped = false;
    try {
      const details = await getCompRankingDetails(
        report,
        this.edition,
        this.refresh,
        this.controller.signal
      );
      if (this.controller.signal.aborted) return false;
      const summaryFight = report.fights?.[0];
      const detailFight = details.fights?.[0];
      if (!detailFight?.composition || !details.players?.length)
        throw new Error('Warcraft Logs returned incomplete composition details.');
      this.loaded.set(key, {
        ...report,
        ...details,
        duration: report.duration,
        startTime: report.startTime,
        fights: [
          {
            ...summaryFight,
            ...detailFight,
            startTime: summaryFight?.startTime ?? report.startTime,
            deaths: summaryFight?.deaths,
            damageTaken: summaryFight?.damageTaken
          }
        ]
      });
      return true;
    } catch (error) {
      if (this.controller.signal.aborted) return false;
      this.errors.set(key, error instanceof Error ? error.message : 'Unable to load kill details.');
      this.stopped = true;
      return false;
    } finally {
      this.loadingKey = '';
    }
  }

  dispose() {
    this.controller.abort();
  }
}

export function matchesCompExpressions(
  counts: Record<string, number>,
  expressions: string[]
): boolean {
  return expressions.every((expression) => {
    const parts = expression.split('.');
    const key = parts[0];
    const op = parts.length === 2 ? 'eq' : parts[1];
    const expected = Number(parts.at(-1));
    const actual = counts[key] ?? 0;
    switch (op) {
      case 'eq':
        return actual === expected;
      case 'lt':
        return actual < expected;
      case 'gt':
        return actual > expected;
      case 'lte':
        return actual <= expected;
      case 'gte':
        return actual >= expected;
      default:
        return false;
    }
  });
}
