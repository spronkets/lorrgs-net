import { describe, expect, it } from 'vitest';
import {
  getApiEditionSlug,
  getAllowedClassIds,
  getAvailableClassesForVersion,
  getAvailableSpecsForVersion,
  getEditionSlug,
  getRaidBossOptions,
  getRaidsForEdition,
  groupRaidsByPhase,
  type RaidCatalog
} from './raidCatalog';
import { getVersionIconUrl } from './selectionIcons';

const sampleCatalog: RaidCatalog = {
  editions: [],
  instances: {
    retail: [
      {
        zoneId: 38,
        slug: 'the-raid',
        name: 'The Raid',
        edition: 'retail',
        phase: 2,
        bosses: [
          { name: 'Boss One', slug: '', mapped: false },
          { name: 'Boss Two', slug: '', mapped: false }
        ],
        public: true
      },
      {
        zoneId: 39,
        slug: 'azuregos',
        name: 'Azuregos',
        edition: 'retail',
        phase: 0,
        bosses: [{ name: 'Azuregos', slug: '', mapped: false }],
        public: true
      }
    ],
    anniversary: [
      {
        zoneId: 1008,
        slug: 'gruulslair',
        name: "Gruul's Lair",
        edition: 'anniversary',
        phase: 1,
        bosses: [{ name: 'High King Maulgar', slug: '', mapped: false }],
        public: true
      }
    ],
    classic: [
      {
        zoneId: 40,
        slug: 'molten-core',
        name: 'Molten Core',
        edition: 'classic',
        phase: 1,
        bosses: [{ name: 'Lucifron', slug: '', mapped: false }],
        public: true
      }
    ]
  }
};

describe('raid catalog version mapping', () => {
  it('maps Retail to the retail edition slug', () => {
    expect(getEditionSlug('Retail')).toBe('retail');
    expect(getEditionSlug('Anniversary')).toBe('anniversary');
    expect(getEditionSlug('Unknown')).toBe('');
  });

  it('maps API editions separately for Anniversary and Era', () => {
    expect(getApiEditionSlug('Anniversary')).toBe('anniversary');
    expect(getApiEditionSlug('Era')).toBe('era');
    expect(getApiEditionSlug('Retail')).toBe('midnight');
    expect(getApiEditionSlug('Unknown')).toBe('');
  });

  it('uses the retail icon for retail selections', () => {
    expect(getVersionIconUrl('Retail')).toBe('/images/wow/editions/midnight.png');
  });

  it('returns the raids for a matching edition slug', () => {
    expect(getRaidsForEdition(sampleCatalog, 'Retail')).toHaveLength(2);
    expect(getRaidsForEdition(sampleCatalog, 'Anniversary')).toHaveLength(1);
    expect(getRaidsForEdition(sampleCatalog, 'Era')).toHaveLength(1);
    expect(getRaidsForEdition(sampleCatalog, 'Unknown')).toEqual([]);
  });

  it('returns configured bosses', () => {
    const raid = sampleCatalog.instances.retail[0];

    expect(getRaidBossOptions(raid)).toEqual([
      { name: 'Boss One', slug: 'boss-one', mapped: false },
      { name: 'Boss Two', slug: 'boss-two', mapped: false }
    ]);
    expect(getRaidBossOptions(null)).toEqual([]);
  });

  it('groups raids by phase and keeps world bosses in a separate group', () => {
    const groups = groupRaidsByPhase(sampleCatalog.instances.retail);

    expect(groups.map((group) => group.label)).toEqual(['Phase 2', 'World Bosses']);
    expect(groups[0].raids[0].name).toBe('The Raid');
    expect(groups[1].raids[0].name).toBe('Azuregos');
  });

  it('keeps the Retail class roster aligned with the current expansion', () => {
    expect(getAllowedClassIds('Retail')).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13]);
    expect(getAllowedClassIds('Unknown')).toEqual([]);
  });

  it('filters classes and specs by the supported Retail version', () => {
    const classes = [{ id: 1 }, { id: 2 }, { id: 6 }, { id: 10 }, { id: 11 }];
    const specs = [
      { id: 101, classId: 1 },
      { id: 102, classId: 2 },
      { id: 103, classId: 6 },
      { id: 104, classId: 10 },
      { id: 105, classId: 11 }
    ];

    expect(getAvailableClassesForVersion(classes, 'Retail')).toEqual([
      { id: 1 },
      { id: 2 },
      { id: 6 },
      { id: 10 },
      { id: 11 }
    ]);
    expect(getAvailableClassesForVersion(classes, 'Unknown')).toEqual(classes);
    expect(getAvailableSpecsForVersion(specs, 'Retail')).toEqual([
      { id: 101, classId: 1 },
      { id: 102, classId: 2 },
      { id: 103, classId: 6 },
      { id: 104, classId: 10 },
      { id: 105, classId: 11 }
    ]);
    expect(getAvailableSpecsForVersion(specs, 'Unknown')).toEqual(specs);
  });
});
