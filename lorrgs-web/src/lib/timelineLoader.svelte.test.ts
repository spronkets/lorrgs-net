import { beforeEach, describe, expect, it, vi } from 'vitest';
import { waitFor } from '@testing-library/svelte';
import { TimelineLoader } from './timelineLoader.svelte';
import { getReportTimeline } from './api';
import type { ReportTimeline } from './models/ReportTimeline';
import { rows, response } from './testing/timelineFixtures';
import { timelineKey } from './timelineUtils';

vi.mock('./api', () => ({ getReportTimeline: vi.fn() }));
beforeEach(() => {
  vi.resetAllMocks();
  vi.mocked(getReportTimeline).mockImplementation(async (report) => response(report));
});

describe('TimelineLoader', () => {
  it('displays cached rows outside the top three without making live requests for them', async () => {
    const reports = rows.map(({ report }, index) => ({
      ...report,
      timeline: [1, 3, 4].includes(index) ? response(report) : undefined
    }));
    const loader = new TimelineLoader(() => 'anniversary');
    try {
      loader.setReports(reports);
      expect(loader.loaded.size).toBe(3);
      expect(getReportTimeline).not.toHaveBeenCalled();
      loader.start();
      await waitFor(() => expect(loader.batchLoading).toBe(false));
      expect(loader.loaded.size).toBe(5);
      expect(vi.mocked(getReportTimeline).mock.calls.map(([report]) => report.reportId)).toEqual([
        'Report0',
        'Report2'
      ]);
      loader.setReports([...reports].reverse());
      expect(getReportTimeline).toHaveBeenCalledTimes(2);
    } finally {
      loader.dispose();
    }
  });

  it('uses bundled timelines for newly visible rows even when the top three are loaded', async () => {
    const loader = new TimelineLoader(() => 'anniversary');
    try {
      loader.setReports(rows.slice(0, 3).map(({ report }) => report));
      loader.start();
      await waitFor(() => expect(loader.batchLoading).toBe(false));
      loader.setReports(
        rows.map(({ report }, index) => ({
          ...report,
          timeline: index === 4 ? response(report) : undefined
        }))
      );
      await waitFor(() => expect(loader.loaded.has(timelineKey(rows[4].report))).toBe(true));
      expect(loader.loaded.has(timelineKey(rows[3].report))).toBe(false);
      expect(getReportTimeline).toHaveBeenCalledTimes(3);
    } finally {
      loader.dispose();
    }
  });

  it('does not request any timelines when all rows are bundled and ignores incomplete bundled data', async () => {
    const loader = new TimelineLoader(() => 'anniversary');
    try {
      loader.setReports(rows.map(({ report }) => ({ ...report, timeline: response(report) })));
      loader.start();
      expect(loader.loaded.size).toBe(5);
      expect(getReportTimeline).not.toHaveBeenCalled();
      const report = { ...rows[0].report, reportId: 'PartialBundled' };
      loader.setReports([{ ...report, timeline: { ...response(report), isComplete: false } }]);
      await waitFor(() => expect(getReportTimeline).toHaveBeenCalledTimes(1));
    } finally {
      loader.dispose();
    }
  });

  it('automatically loads only three and manually loads the next batch without repeating requests', async () => {
    const loader = new TimelineLoader(() => 'Anniversary');
    try {
      const reports = [
        ...rows.map(({ report }) => report),
        ...rows.slice(0, 3).map(({ report }, index) => ({ ...report, reportId: `Extra${index}` }))
      ];
      loader.setReports(reports);
      loader.start();
      await waitFor(() => expect(loader.batchLoading).toBe(false));
      expect(loader.loaded.size).toBe(3);
      expect(getReportTimeline).toHaveBeenCalledTimes(3);
      await loader.loadNext();
      expect(loader.loaded.size).toBe(6);
      expect(getReportTimeline).toHaveBeenCalledTimes(6);
      loader.setReports(reports);
      await loader.loadNext();
      expect(loader.loaded.size).toBe(8);
      await loader.loadNext();
      expect(getReportTimeline).toHaveBeenCalledTimes(8);
    } finally {
      loader.dispose();
    }
  });

  it('reprioritizes the automatic queue between sequential requests', async () => {
    let resolve!: (timeline: ReportTimeline) => void;
    vi.mocked(getReportTimeline).mockImplementationOnce(
      () =>
        new Promise((done) => {
          resolve = done;
        })
    );
    const loader = new TimelineLoader(() => 'Anniversary');
    try {
      loader.setReports(rows.map(({ report }) => report));
      loader.start();
      await waitFor(() => expect(getReportTimeline).toHaveBeenCalledTimes(1));
      loader.setReports([rows[4].report, rows[3].report, rows[2].report]);
      resolve(response(rows[0].report));
      await waitFor(() => expect(loader.batchLoading).toBe(false));
      expect(vi.mocked(getReportTimeline).mock.calls.map(([report]) => report.reportId)).toEqual([
        'Report0',
        'Report4',
        'Report3',
        'Report2'
      ]);
    } finally {
      loader.dispose();
    }
  });

  it('stops after an error and explicitly retries before resuming the automatic batch', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    vi.mocked(getReportTimeline).mockRejectedValueOnce(new Error('Rate limited'));
    const loader = new TimelineLoader(() => 'Anniversary');
    try {
      loader.setReports(rows.map(({ report }) => report));
      loader.start();
      await waitFor(() => expect(loader.stopped).toBe(true));
      expect(getReportTimeline).toHaveBeenCalledTimes(1);
      expect(loader.errors.get(timelineKey(rows[0].report))).toBe('Rate limited');
      await loader.loadOne(rows[0].report);
      await waitFor(() => expect(loader.batchLoading).toBe(false));
      expect(loader.loaded.size).toBe(3);
      expect(loader.stopped).toBe(false);
      expect(loader.errors.size).toBe(0);
    } finally {
      loader.dispose();
      vi.restoreAllMocks();
    }
  });

  it('aborts in-flight work on disposal and never starts another request', async () => {
    let resolve!: (timeline: ReportTimeline) => void;
    vi.mocked(getReportTimeline).mockImplementationOnce(
      () =>
        new Promise((done) => {
          resolve = done;
        })
    );
    const loader = new TimelineLoader(() => 'Anniversary');
    loader.setReports(rows.map(({ report }) => report));
    loader.start();
    await waitFor(() => expect(getReportTimeline).toHaveBeenCalledTimes(1));
    const signal = vi.mocked(getReportTimeline).mock.calls[0][2]!;
    loader.dispose();
    expect(signal.aborted).toBe(true);
    resolve(response(rows[0].report));
    await waitFor(() => expect(loader.batchLoading).toBe(false));
    expect(loader.loaded.size).toBe(0);
    expect(getReportTimeline).toHaveBeenCalledTimes(1);
  });
});
