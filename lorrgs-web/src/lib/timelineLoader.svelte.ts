import { SvelteMap, SvelteSet } from 'svelte/reactivity';
import { getReportTimeline, type RankingReport, type ReportTimeline } from './api';
import { canLoadTimeline, timelineKey } from './timelineUtils';

export class TimelineLoader {
  readonly loaded = new SvelteMap<string, ReportTimeline>();
  readonly errors = new SvelteMap<string, string>();
  loadingKey = $state('');
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
      this.queue.slice(0, 3).some((report) => !this.loaded.has(timelineKey(report)))
    )
      void this.loadNext(true);
  }

  private async load(report: RankingReport): Promise<boolean> {
    const key = timelineKey(report);
    this.loadingKey = key;
    this.errors.delete(key);
    try {
      const timeline = await getReportTimeline(report, this.getEdition(), this.controller.signal);
      if (this.controller.signal.aborted) return false;
      this.loaded.set(key, timeline);
      return true;
    } catch (error) {
      if (this.controller.signal.aborted) return false;
      this.errors.set(key, error instanceof Error ? error.message : 'Unable to load timeline.');
      this.stopped = true;
      console.error('Ranking timeline failed; remaining automatic loads stopped', {
        reportCode: report.reportId,
        fightId: report.fights?.[0]?.fightId,
        edition: this.getEdition(),
        error
      });
      return false;
    } finally {
      this.loadingKey = '';
    }
  }

  async loadNext(automatic = false) {
    if (this.batchLoading || this.controller.signal.aborted) return;
    this.batchLoading = true;
    this.stopped = false;
    try {
      const requestedKeys = automatic
        ? null
        : new SvelteSet(this.pending.slice(0, 3).map(timelineKey));
      while (!this.controller.signal.aborted) {
        const keys = requestedKeys ?? new SvelteSet(this.queue.slice(0, 3).map(timelineKey));
        const next = this.queue.find(
          (report) => !this.loaded.has(timelineKey(report)) && keys.has(timelineKey(report))
        );
        if (!next || !(await this.load(next))) break;
      }
    } finally {
      this.batchLoading = false;
      this.loadAutomatically();
    }
  }

  async loadOne(report: RankingReport) {
    if (this.batchLoading || this.controller.signal.aborted) return;
    this.batchLoading = true;
    this.stopped = false;
    try {
      await this.load(report);
    } finally {
      this.batchLoading = false;
      this.loadAutomatically();
    }
  }
}
