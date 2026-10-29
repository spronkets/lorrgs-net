import { SvelteMap, SvelteSet } from 'svelte/reactivity';
import { getReportTimeline } from './api';
import type { RankingReport } from './models/RankingReport';
import type { ReportTimeline } from './models/ReportTimeline';
import { canLoadTimeline, timelineKey } from './timelineUtils';

export class TimelineLoader {
  readonly loaded = new SvelteMap<string, ReportTimeline>();
  readonly errors = new SvelteMap<string, string>();
  readonly loadingKeys = new SvelteSet<string>();
  batchLoading = $state(false);
  stopped = $state(false);
  private queue: RankingReport[] = [];
  private active = false;
  private readonly controller = new AbortController();

  constructor(private readonly getEdition: () => string) {}

  get pending() {
    return this.queue.filter((report) => !this.loaded.has(timelineKey(report)));
  }

  setReports(reports: RankingReport[]) {
    this.queue = reports.filter(canLoadTimeline);
    for (const report of this.queue) {
      if (report.timeline?.isComplete && !this.loaded.has(timelineKey(report)))
        this.loaded.set(timelineKey(report), report.timeline);
    }
    this.loadAutomatically();
  }

  start() {
    this.active = true;
    this.loadAutomatically();
  }

  dispose() {
    this.active = false;
    this.controller.abort();
  }

  private loadAutomatically() {
    if (
      this.active &&
      !this.batchLoading &&
      !this.stopped &&
      this.queue
        .slice(0, 3)
        .some(
          (report) =>
            !this.loaded.has(timelineKey(report)) && !this.loadingKeys.has(timelineKey(report))
        )
    )
      void this.loadNext(true);
  }

  private async load(report: RankingReport): Promise<boolean> {
    const key = timelineKey(report);
    if (this.loadingKeys.has(key) || this.loaded.has(key) || this.controller.signal.aborted)
      return false;
    this.loadingKeys.add(key);
    this.errors.delete(key);
    try {
      const timeline = await getReportTimeline(report, this.getEdition(), this.controller.signal);
      if (this.controller.signal.aborted) return false;
      this.loaded.set(key, timeline);
      return true;
    } catch (error: unknown) {
      if (this.controller.signal.aborted) return false;
      this.errors.set(key, error instanceof Error ? error.message : 'Unable to load timeline.');
      this.stopped = true;
      console.error('Ranking timeline failed; further automatic batches stopped', {
        reportCode: report.reportId,
        fightId: report.fights?.[0]?.fightId,
        edition: this.getEdition(),
        error
      });
      return false;
    } finally {
      this.loadingKeys.delete(key);
    }
  }

  async loadNext(automatic = false) {
    if (this.batchLoading || this.controller.signal.aborted) return;
    this.batchLoading = true;
    this.stopped = false;
    try {
      const reports = automatic
        ? this.queue
            .slice(0, 3)
            .filter(
              (report) =>
                !this.loaded.has(timelineKey(report)) && !this.loadingKeys.has(timelineKey(report))
            )
        : this.pending.filter((report) => !this.loadingKeys.has(timelineKey(report))).slice(0, 3);
      await Promise.all(reports.map((report) => this.load(report)));
    } finally {
      this.stopped = this.errors.size > 0;
      this.batchLoading = false;
      this.loadAutomatically();
    }
  }

  async loadOne(report: RankingReport) {
    if (this.controller.signal.aborted) return;
    try {
      await this.load(report);
    } finally {
      this.stopped = this.errors.size > 0;
      this.loadAutomatically();
    }
  }
}
