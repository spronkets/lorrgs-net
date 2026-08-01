import { describe, expect, it } from 'vitest'
import {
  getAllowedClassIds,
  getAvailableClassesForVersion,
  getAvailableSpecsForVersion,
  getEditionSlug,
  getRaidBossOptions,
  getRaidsForVersion,
  groupRaidsByPhase,
  type RaidCatalog
} from './raidCatalog'
import { getVersionIconUrl } from './selectionIcons'

const sampleCatalog: RaidCatalog = {
  editions: [],
  instances: {
    midnight: [
      {
        id: 1,
        slug: 'the-raid',
        name: 'The Raid',
        edition: 'midnight',
        phase: 2,
        bosses: [
          { name: 'Boss One', slug: '', mapped: false },
          { name: 'Boss Two', slug: '', mapped: false }
        ],
        public: true
      },
      {
        id: 2,
        slug: 'azuregos',
        name: 'Azuregos',
        edition: 'midnight',
        phase: 0,
        bosses: [{ name: 'Azuregos', slug: '', mapped: false }],
        public: true
      }
    ],
    classic: [
      {
        id: 3,
        slug: 'molten-core',
        name: 'Molten Core',
        edition: 'classic',
        phase: 1,
        bosses: [{ name: 'Lucifron', slug: '', mapped: false }],
        public: true
      }
    ]
  }
}

describe('raid catalog version mapping', () => {
  it('maps Retail to the current Midnight edition slug', () => {
    expect(getEditionSlug('Retail')).toBe('midnight')
    expect(getEditionSlug('Midnight')).toBe('midnight')
    expect(getEditionSlug('Unknown')).toBe('')
  })

  it('uses the Midnight icon for retail selections', () => {
    expect(getVersionIconUrl('Retail')).toBe('/images/wow/editions/midnight.png')
  })

  it('returns the raids for a matching edition slug', () => {
    expect(getRaidsForVersion(sampleCatalog, 'Retail')).toHaveLength(2)
    expect(getRaidsForVersion(sampleCatalog, 'Era')).toHaveLength(1)
    expect(getRaidsForVersion(sampleCatalog, 'Unknown')).toEqual([])
  })

  it('returns configured bosses', () => {
    const raid = sampleCatalog.instances.midnight[0]

    expect(getRaidBossOptions(raid)).toEqual([
      { name: 'Boss One', slug: '', mapped: false },
      { name: 'Boss Two', slug: '', mapped: false }
    ])
    expect(getRaidBossOptions(null)).toEqual([])
  })

  it('groups raids by phase and keeps world bosses in a separate group', () => {
    const groups = groupRaidsByPhase(sampleCatalog.instances.midnight)

    expect(groups.map((group) => group.label)).toEqual(['Phase 2', 'World Bosses'])
    expect(groups[0].raids[0].name).toBe('The Raid')
    expect(groups[1].raids[0].name).toBe('Azuregos')
  })

  it('keeps the Midnight class roster aligned with the current expansion', () => {
    expect(getAllowedClassIds('Midnight')).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13])
    expect(getAllowedClassIds('Unknown')).toEqual([])
  })

  it('filters classes and specs by the supported Midnight version', () => {
    const classes = [{ id: 1 }, { id: 2 }, { id: 6 }, { id: 10 }, { id: 11 }]
    const specs = [
      { id: 101, classId: 1 },
      { id: 102, classId: 2 },
      { id: 103, classId: 6 },
      { id: 104, classId: 10 },
      { id: 105, classId: 11 }
    ]

    expect(getAvailableClassesForVersion(classes, 'Midnight')).toEqual([
      { id: 1 },
      { id: 2 },
      { id: 6 },
      { id: 10 },
      { id: 11 }
    ])
    expect(getAvailableClassesForVersion(classes, 'Unknown')).toEqual(classes)
    expect(getAvailableSpecsForVersion(specs, 'Midnight')).toEqual([
      { id: 101, classId: 1 },
      { id: 102, classId: 2 },
      { id: 103, classId: 6 },
      { id: 104, classId: 10 },
      { id: 105, classId: 11 }
    ])
    expect(getAvailableSpecsForVersion(specs, 'Unknown')).toEqual(specs)
  })
})
