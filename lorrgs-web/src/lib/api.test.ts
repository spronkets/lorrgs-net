import { afterEach, describe, expect, it, vi } from 'vitest';
import { getReportTimeline, getBossTimeline, getSpecRankings, getCompRankings } from './api';
import { rows, response } from './testing/timelineFixtures';

afterEach(() => vi.unstubAllGlobals());

describe('api utilities', () => {
  it('sends complete composition context, repeated comparisons, pagination and refresh', async () => {
    const fetch = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ reports: [] }) });
    vi.stubGlobal('fetch', fetch);
    const controller = new AbortController();
    await getCompRankings(
      'boss slug',
      ['heal.gte.2', 'tank.lte.2'],
      ['holy-paladin.eq.0'],
      'anniversary',
      {
        encounterId: 50609,
        difficultyId: 3,
        page: 2,
        minSeconds: 120,
        maxSeconds: 600,
        refresh: true
      },
      controller.signal
    );
    const [url, options] = fetch.mock.calls[0];
    const parsed = new URL(String(url), 'http://localhost');
    expect(parsed.pathname).toBe('/api/rankings/comp/boss%20slug');
    expect(parsed.searchParams.getAll('role')).toEqual(['heal.gte.2', 'tank.lte.2']);
    expect(parsed.searchParams.getAll('spec')).toEqual(['holy-paladin.eq.0']);
    expect(Object.fromEntries(parsed.searchParams)).toMatchObject({
      edition: 'anniversary',
      encounterId: '50609',
      difficultyId: '3',
      page: '2',
      killtime_min: '120',
      killtime_max: '600',
      refresh: 'true'
    });
    expect(options.signal).toBeInstanceOf(AbortSignal);
    expect(options.timeoutMs).toBeUndefined();
    expect(parsed.searchParams.has('limit')).toBe(false);
  });

  it('requests the selected fight boss reference from the API without browser caching', async () => {
    const fetch = vi
      .fn()
      .mockResolvedValue({ ok: true, json: async () => ({ phases: [], events: [] }) });
    vi.stubGlobal('fetch', fetch);
    await getBossTimeline(rows[0].report, 'anniversary');
    await getBossTimeline(rows[0].report, 'anniversary');
    expect(fetch).toHaveBeenCalledTimes(2);
    expect(fetch.mock.calls[0][0]).toContain('fightId=1&edition=anniversary');
    expect(fetch.mock.calls[0][0]).toContain('/timeline/boss?');
  });
  it('uses the API for each timeline request rather than retaining responses in a browser cache', async () => {
    const report = rows[0].report;
    const timeline = response(report);
    const fetch = vi.fn().mockResolvedValue({ ok: true, json: async () => timeline });
    vi.stubGlobal('fetch', fetch);
    expect(await getReportTimeline(report, 'anniversary')).toEqual(timeline);
    expect(await getReportTimeline(report, 'anniversary')).toEqual(timeline);
    expect(fetch).toHaveBeenCalledTimes(2);
    expect(fetch.mock.calls[0][0]).toContain(
      '/warcraftlogs/reports/Report0/timeline?fightId=1&edition=anniversary&playerName=Player0'
    );
    await getReportTimeline(report, 'retail');
    expect(fetch.mock.calls[2][0]).toContain('edition=retail');
    expect(fetch.mock.calls[2][0]).toContain('specId=70');
  });

  it('retains cached timelines included in the initial rankings response without extra requests', async () => {
    const ranking = {
      reports: rows.map(({ report }) => ({ ...report, timeline: response(report) }))
    };
    const fetch = vi.fn().mockResolvedValue({ ok: true, json: async () => ranking });
    vi.stubGlobal('fetch', fetch);
    const result = await getSpecRankings('retribution-paladin', 'illidan-stormrage');
    expect(result.reports).toEqual(ranking.reports);
    expect(fetch).toHaveBeenCalledTimes(1);
  });

  it('propagates errors and cancellation instead of falling back to browser data', async () => {
    const fetch = vi.fn().mockRejectedValue(new Error('Network failed'));
    vi.stubGlobal('fetch', fetch);
    await expect(getReportTimeline(rows[0].report, 'anniversary')).rejects.toThrow(
      'Network failed'
    );
    const controller = new AbortController();
    controller.abort();
    await expect(
      getReportTimeline(rows[0].report, 'anniversary', controller.signal)
    ).rejects.toThrow('Aborted');
    expect(fetch).toHaveBeenCalledTimes(1);
  });

  it('shows upstream report availability details from Problem Details responses', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: false,
        status: 403,
        statusText: 'Forbidden',
        headers: new Headers({ 'Content-Type': 'application/problem+json' }),
        json: async () => ({ detail: 'This Warcraft Logs report is private or inaccessible.' })
      })
    );
    await expect(getReportTimeline(rows[0].report, 'retail')).rejects.toThrow(
      'This Warcraft Logs report is private or inaccessible.'
    );
  });
});
