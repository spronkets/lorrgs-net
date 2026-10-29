import { afterEach, describe, expect, it, vi } from 'vitest';
import { buildApiUrl, get, send, normalizeApiCollection, normalizeApiPayload } from './apiClient';

afterEach(() => vi.unstubAllGlobals());

describe('HTTP transport', () => {
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
  it('builds URLs relative to the configured API base', () => {
    expect(buildApiUrl('/specs')).toMatch(/\/specs$/);
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
