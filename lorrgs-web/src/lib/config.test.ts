import { afterEach, expect, it, vi } from 'vitest';

afterEach(() => {
  vi.unstubAllEnvs();
  vi.resetModules();
});

it('defaults the request timeout to two minutes', async () => {
  vi.stubEnv('VITE_REQUEST_TIMEOUT_MS', undefined);
  vi.resetModules();
  expect((await import('./config')).config.requestTimeoutMs).toBe(120000);
});

it('reads the configured request timeout', async () => {
  vi.stubEnv('VITE_REQUEST_TIMEOUT_MS', '240000');
  vi.resetModules();
  expect((await import('./config')).config.requestTimeoutMs).toBe(240000);
});

it.each(['', 'abc', '0', '-1', '1.5', 'Infinity', '9007199254740992'])(
  'rejects invalid request timeout %j',
  async (value: string) => {
    vi.stubEnv('VITE_REQUEST_TIMEOUT_MS', value);
    vi.resetModules();
    await expect(import('./config')).rejects.toThrow(
      'VITE_REQUEST_TIMEOUT_MS must be a positive integer in milliseconds.'
    );
  }
);
