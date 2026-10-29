import { afterEach, describe, expect, it, vi } from 'vitest';
import { buildApiUrl, get, send, normalizeApiCollection, normalizeApiPayload } from './apiClient';

afterEach(() => {
  vi.unstubAllGlobals();
  vi.unstubAllEnvs();
  vi.resetModules();
  vi.useRealTimers();
});

describe.each(['get', 'send'] as const)('%s cancellation', (method) => {
  const load = (signal: AbortSignal, timeoutMs?: number) =>
    method === 'get'
      ? get('/rankings', { signal, timeoutMs })
      : send('/rankings', {}, { signal, timeoutMs });

  it('uses the shared configured timeout for non-ranking requests', async () => {
    vi.stubEnv('VITE_REQUEST_TIMEOUT_MS', '5000');
    vi.resetModules();
    const api = await import('./apiClient');
    vi.useFakeTimers();
    const parent = new AbortController();
    vi.stubGlobal(
      'fetch',
      vi.fn(
        (_url: string, options: RequestInit) =>
          new Promise<Response>((_resolve, reject) => {
            options.signal!.addEventListener('abort', () =>
              reject(new DOMException('Aborted', 'AbortError'))
            );
          })
      )
    );
    const result =
      method === 'get'
        ? api.get('/worlddata/classes', { signal: parent.signal })
        : api.send('/warcraftlogs/rotation/analyze', {}, { signal: parent.signal });
    const assertion = expect(result).rejects.toThrow('request timed out after 5000ms');
    await vi.advanceTimersByTimeAsync(4999);
    expect(vi.getTimerCount()).toBe(1);
    await vi.advanceTimersByTimeAsync(1);
    await assertion;
    expect(parent.signal.aborted).toBe(false);
    expect(vi.getTimerCount()).toBe(0);
  });

  it('cancels every concurrent request when its parent is aborted', async () => {
    const signals: AbortSignal[] = [];
    vi.stubGlobal(
      'fetch',
      vi.fn(
        (_url: string, options: RequestInit) =>
          new Promise((_resolve, reject) => {
            const signal = options.signal!;
            signals.push(signal);
            signal.addEventListener('abort', () =>
              reject(new DOMException('Aborted', 'AbortError'))
            );
          })
      )
    );
    const parent = new AbortController();
    const result = Promise.allSettled([
      load(parent.signal),
      load(parent.signal),
      load(parent.signal)
    ]);
    parent.abort();
    expect(signals).toHaveLength(3);
    expect(signals.every((signal) => signal.aborted)).toBe(true);
    for (const outcome of await result) {
      expect(outcome).toMatchObject({ status: 'rejected', reason: { name: 'AbortError' } });
    }
  });

  it.each([200, 502])(
    'keeps cancellation connected while reading a %s response body',
    async (status) => {
      const reading = vi.fn();
      vi.stubGlobal(
        'fetch',
        vi.fn(
          async (_url: string, options: RequestInit) =>
            new Response(
              new ReadableStream({
                start(controller) {
                  options.signal!.addEventListener('abort', () =>
                    controller.error(new DOMException('Aborted', 'AbortError'))
                  );
                },
                pull() {
                  reading();
                }
              }),
              { status, headers: { 'content-type': 'application/json' } }
            )
        )
      );
      const parent = new AbortController();
      const result = expect(load(parent.signal)).rejects.toMatchObject({ name: 'AbortError' });
      await vi.waitFor(() => expect(reading).toHaveBeenCalled());
      parent.abort();
      await result;
    }
  );

  it('preserves the timeout when a parent signal is also supplied', async () => {
    vi.useFakeTimers();
    vi.stubGlobal(
      'fetch',
      vi.fn(
        (_url: string, options: RequestInit) =>
          new Promise((_resolve, reject) =>
            options.signal!.addEventListener('abort', () =>
              reject(new DOMException('Aborted', 'AbortError'))
            )
          )
      )
    );
    const parent = new AbortController();
    const result = expect(load(parent.signal, 100)).rejects.toThrow(
      'request timed out after 100ms'
    );
    await vi.advanceTimersByTimeAsync(100);
    await result;
    expect(parent.signal.aborted).toBe(false);
  });
});

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
