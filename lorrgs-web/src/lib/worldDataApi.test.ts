import { afterEach, describe, expect, it, vi } from 'vitest';
import {
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
  getTrinket
} from './worldDataApi';

afterEach(() => vi.unstubAllGlobals());

describe('World Data API', () => {
  it('normalizes each collection without changing item responses', async () => {
    const fetch = vi
      .fn()
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ paladin: { id: 2, name: 'Paladin' } })
      })
      .mockResolvedValueOnce({ ok: true, json: async () => ({ roles: [{ id: 1, name: 'Tank' }] }) })
      .mockResolvedValueOnce({ ok: true, json: async () => ({ id: 7, nameSlug: 'boss' }) });
    vi.stubGlobal('fetch', fetch);
    await expect(getClasses('retail')).resolves.toEqual([{ id: 2, name: 'Paladin' }]);
    await expect(getRoles('retail')).resolves.toEqual([{ id: 1, name: 'Tank' }]);
    await expect(getBoss('boss', 'retail')).resolves.toEqual({ id: 7, nameSlug: 'boss' });
  });

  it('propagates unavailable data instead of returning an empty catalog', async () => {
    const fetch = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ error: 'Boss not found: missing' }), {
        status: 404,
        headers: { 'content-type': 'application/json' }
      })
    );
    vi.stubGlobal('fetch', fetch);
    await expect(getBoss('missing', 'retail')).rejects.toThrow('Boss not found: missing');
    expect(fetch).toHaveBeenCalledTimes(1);
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
});
