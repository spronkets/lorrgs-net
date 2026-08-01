import { describe, expect, it } from 'vitest';
import { getDisplayPercentile } from './rankingUtils';

describe('ranking utilities', () => {
  it('formats percentile values with a safe fallback', () => {
    expect(getDisplayPercentile(81.234)).toBe('81.2%');
    expect(getDisplayPercentile(null)).toBe('N/A');
    expect(getDisplayPercentile(undefined)).toBe('N/A');
  });
});
