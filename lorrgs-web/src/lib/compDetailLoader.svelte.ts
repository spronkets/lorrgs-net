import { SvelteMap, SvelteSet } from 'svelte/reactivity';
import { getCompRankingDetails } from './api';
import type { RankingReport } from './models/RankingReport';

export function compKey(report: RankingReport): string {
  return `${report.reportId}:${report.fights?.[0]?.fightId ?? 0}`;
}

export class CompDetailLoader {
  readonly loaded = new SvelteMap<string, RankingReport>();
  readonly errors = new SvelteMap<string, string>();
  readonly loadingKeys = new SvelteSet<string>();
  stopped = $state(false);
  private readonly controller = new AbortController();

  constructor(
    private readonly edition: string,
    private readonly refresh: boolean
  ) {}

  async start(reports: RankingReport[]) {
    // Exactly the first three kills, not the first three matches. No backfill on failures.
    await Promise.all(reports.slice(0, 3).map((report) => this.load(report)));
  }

  async load(report: RankingReport): Promise<boolean> {
    const key = compKey(report);
    if (this.loadingKeys.has(key) || this.loaded.has(key) || this.controller.signal.aborted)
      return false;
    this.loadingKeys.add(key);
    this.errors.delete(key);
    this.stopped = this.errors.size > 0;
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
            deaths: detailFight.deaths ?? summaryFight?.deaths,
            damageTaken: detailFight.damageTaken ?? summaryFight?.damageTaken
          }
        ]
      });
      return true;
    } catch (error: unknown) {
      if (this.controller.signal.aborted) return false;
      this.errors.set(key, error instanceof Error ? error.message : 'Unable to load kill details.');
      this.stopped = true;
      return false;
    } finally {
      this.loadingKeys.delete(key);
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
