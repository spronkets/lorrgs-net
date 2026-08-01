import { describe, expect, it } from 'vitest';
import { buildApiUrl, normalizeApiCollection, normalizeApiPayload } from './api';

describe('api utilities', () => {
  it('builds URLs relative to the configured API base', () => {
    const url = buildApiUrl('/specs');
    expect(url).toMatch(/\/specs$/);
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
