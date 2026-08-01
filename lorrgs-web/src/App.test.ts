import { describe, expect, it } from 'vitest';
import { getEditionSlug } from './lib/raidCatalog';
import { getVersionIconUrl } from './lib/selectionIcons';

describe('App version mapping', () => {
  it('uses the retail catalog key and retail icon for the Retail option', () => {
    expect(getEditionSlug('Retail')).toBe('retail');
    expect(getVersionIconUrl('Retail')).toBe('/images/wow/editions/midnight.png');
  });
});
