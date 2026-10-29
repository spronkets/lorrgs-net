import { Mask } from 'maska';
import { describe, expect, it } from 'vitest';
import { killTimeMask } from './killTimeMask';

describe('kill-time mask', () => {
  const mask = new Mask(killTimeMask);

  it('inserts the separator and preserves formatted durations', () => {
    expect(mask.masked('532')).toBe('5:32');
    expect(mask.masked('5:32')).toBe('5:32');
    expect(mask.masked('12:30')).toBe('12:30');
    expect(mask.masked('0230')).toBe('02:30');
    expect(mask.masked('02:30')).toBe('02:30');
    expect(mask.masked('100:30')).toBe('100:30');
    expect(mask.masked('')).toBe('');
  });

  it('rejects letters and seconds above 59', () => {
    expect(mask.masked('ab')).toBe('');
    expect(mask.masked('02:69')).not.toBe('02:69');
    expect(mask.masked('02:59')).toBe('02:59');
  });
});
