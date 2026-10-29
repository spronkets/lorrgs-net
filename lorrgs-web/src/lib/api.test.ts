import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  buildApiUrl,
  get,
  send,
  getReportTimeline,
  getBossTimeline,
  getSpecRankings,
  getCompRankings,
  getRoles,
  getClasses,
  getSpecs,
  getBosses,
  getZones,
  getSpells,
  getTrinkets,
  getSeasons,
  getSeason,
  getZone,
  getZoneBosses,
  getBoss,
  getSpell,
  getTrinket,
  normalizeApiCollection,
  normalizeApiPayload
} from './api';
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
  it('passes edition on every world-data collection and individual request', async () => {
    const fetch = vi.fn().mockResolvedValue({ ok: true, json: async () => [] });
    vi.stubGlobal('fetch', fetch);
    for (const edition of ['anniversary', 'era', 'midnight', 'mop']) {
      for (const collection of [
        getRoles,
        getClasses,
        getSpecs,
        getBosses,
        getZones,
        getSpells,
        getTrinkets,
        getSeasons
      ])
        await collection(edition);
      await getBoss('zuljin', edition);
      await getZone('zulaman', edition);
      await getZoneBosses('zulaman', edition);
      await getSeason('season', edition);
      await getSpell(1, edition);
      await getTrinket(1, edition);
      for (const [url] of fetch.mock.calls.slice(-14)) {
        expect(new URL(String(url), 'http://localhost').searchParams.get('edition')).toBe(edition);
      }
    }
    expect(fetch).toHaveBeenCalledTimes(56);
  });

  it('rejects empty world-data editions before sending a request', async () => {
    const fetch = vi.fn();
    vi.stubGlobal('fetch', fetch);
    await expect(getRoles('')).rejects.toThrow('edition is required');
    expect(fetch).not.toHaveBeenCalled();
  });

  it.each(['get', 'send'] as const)(
    'shows a friendly rate-limit message for %s without retrying',
    async (method) => {
      const fetch = vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ error: 'Raw upstream detail', traceId: 'private' }), {
          status: 429,
          statusText: 'Too Many Requests',
          headers: { 'content-type': 'application/json' }
        })
      );
      vi.stubGlobal('fetch', fetch);
      await expect(method === 'get' ? get('/rankings') : send('/rankings', {})).rejects.toThrow(
        "Warcraft Logs needs a breather. We've hit its request limit, so please try again later."
      );
      expect(fetch).toHaveBeenCalledTimes(1);
    }
  );
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

  it('builds URLs relative to the configured API base', () => {
    expect(buildApiUrl('/specs')).toMatch(/\/specs$/);
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

  it('normalizes object payloads into arrays for the UI', () => {
    const payload = {
      first: { id: 1, name: 'Alpha' },
      second: { id: 2, name: 'Beta' }
    };
    expect(normalizeApiPayload(payload)).toEqual([
      { id: 1, name: 'Alpha' },
      { id: 2, name: 'Beta' }
    ]);
  });

  it('normalizes collection payloads from either object maps or wrapped arrays', () => {
    expect(normalizeApiCollection({ classes: [{ id: 1 }] }, 'classes')).toEqual([{ id: 1 }]);
    expect(normalizeApiCollection({ first: { id: 1 }, second: { id: 2 } })).toEqual([
      { id: 1 },
      { id: 2 }
    ]);
  });
});
