import { expect, it } from 'vitest';
import { getBossPortraitUrl } from './bossPortraits';

it('resolves every Black Temple boss including journal filename aliases', () => {
  for (const slug of [
    'high-warlord-najentus',
    'supremus',
    'shade-of-akama',
    'teron-gorefiend',
    'gurtogg-bloodboil',
    'reliquary-of-souls',
    'mother-shahraz',
    'the-illidari-council',
    'illidan-stormrage'
  ]) {
    expect(getBossPortraitUrl(slug)).toMatch(
      /^https:\/\/wow\.zamimg\.com\/images\/wow\/journal\/ui-ej-boss-.+\.png$/
    );
  }
  expect(getBossPortraitUrl('gurtogg-bloodboil')).toContain('gurtoggbloodboil.png');
  expect(getBossPortraitUrl('unknown')).toBe('');
});
