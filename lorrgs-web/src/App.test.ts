import { describe, expect, it } from 'vitest'
import { getEditionSlug } from './lib/raidCatalog'
import { getVersionIconUrl } from './lib/selectionIcons'

describe('App version mapping', () => {
  it('uses the midnight catalog and icon for the Retail option', () => {
    expect(getEditionSlug('Retail')).toBe('midnight')
    expect(getVersionIconUrl('Retail')).toBe('/images/wow/editions/midnight.png')
  })
})
