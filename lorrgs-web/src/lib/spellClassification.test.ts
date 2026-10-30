import { describe, expect, it } from 'vitest';
import { getClassRoleIconUrl } from './wowAssets';
import type { TimelineEvent } from './models/TimelineEvent';
import {
  matchesSpellPreset,
  classSpellPresets,
  matchesClassSpellPreset,
  withResourceLanes
} from './spellClassification';

describe('spell classification', () => {
  it('keeps outgoing buffs and consumables above when casts are hidden without duplicates', () => {
    const spells = [
      { id: 1, name: 'Avenging Wrath', icon: null },
      { id: 2, name: 'Ironshield Potion', icon: null },
      { id: 3, name: 'Bloodlust', icon: null }
    ];
    const allEvents: TimelineEvent[] = [
      { spellId: 1, timestamp: 0, duration: null, kind: 'cast' },
      { spellId: 1, timestamp: 0, duration: 20000, kind: 'buff' },
      { spellId: 2, timestamp: 1000, duration: null, kind: 'cast' },
      { spellId: 2, timestamp: 1000, duration: 120000, kind: 'buff' },
      { spellId: 3, timestamp: 2000, duration: 40000, kind: 'buff' }
    ];
    const combined = withResourceLanes(allEvents, spells, allEvents, true);
    expect(combined).toHaveLength(4);
    expect(combined.filter((event) => event.spellId === 2)).toHaveLength(1);
    expect(combined.find((event) => event.spellId === 3)?.displayBelow).toBe(true);
    const hiddenCasts = withResourceLanes(allEvents.filter((event) => event.kind === 'buff'), spells, allEvents);
    expect(hiddenCasts.find((event) => event.spellId === 1)?.displayBelow).toBe(false);
    expect(hiddenCasts.find((event) => event.spellId === 2)?.displayBelow).toBe(false);
    expect(hiddenCasts.find((event) => event.spellId === 3)?.displayBelow).toBe(false);
  });
  it('keeps self healing and mana abilities above but the same spell from another player below', () => {
    const events: TimelineEvent[] = [
      { spellId: 1, timestamp: 0, duration: null, kind: 'cast' },
      { spellId: 1, timestamp: 0, duration: null, kind: 'heal', sourceName: 'Alice' },
      { spellId: 1, timestamp: 1000, duration: null, kind: 'heal', sourceName: 'Bob' },
      { spellId: 2, timestamp: 1000, duration: null, kind: 'mana-returned', sourceName: 'Alice' },
      { spellId: 2, timestamp: 2000, duration: null, kind: 'mana-returned', sourceName: 'Bob' },
      { spellId: 1, timestamp: 3000, duration: null, kind: 'damage', sourceName: 'Alice' }
    ];
    expect(withResourceLanes(events, [], events, true, 'Alice').map((event) => event.displayBelow))
      .toEqual([false, false, true, false, true, true]);
  });
  it('keeps defensive casts and buffs above regardless of incoming-heal visibility', () => {
    const spells = [
      { id: 1, name: 'Shield Wall', icon: null },
      { id: 2, name: 'Pain Suppression', icon: null },
      { id: 3, name: 'Bloodlust', icon: null }
    ];
    const events: TimelineEvent[] = [
      { spellId: 1, timestamp: 0, duration: null, kind: 'cast' },
      { spellId: 1, timestamp: 0, duration: 10000, kind: 'buff' },
      { spellId: 2, timestamp: 0, duration: 10000, kind: 'buff' },
      { spellId: 3, timestamp: 0, duration: 10000, kind: 'buff' }
    ];
    for (const enabled of [false, true]) {
      const lanes = withResourceLanes(events, spells, events, enabled);
      expect(lanes).toHaveLength(4);
      expect(lanes.filter((event) => event.spellId !== 3).every((event) => !event.displayBelow)).toBe(true);
      expect(lanes.find((event) => event.spellId === 3)?.displayBelow).toBe(enabled);
    }
  });
  it('uses cross-class raid cooldowns without changing spec-ranking presets', () => {
    const presets = classSpellPresets(0, 'anniversary', true);
    expect(presets.map((preset) => preset.label)).toEqual([
      'Everything',
      'Raid cooldowns',
      'Healing',
      'Tank',
      'Consumables'
    ]);
    const raid = presets.find((preset) => preset.key === 'RaidCooldowns')!;
    for (const [id, name] of [
      [31821, 'Aura Mastery'],
      [97462, 'Rallying Cry'],
      [1, 'Tranquility (Rank 5)']
    ] as const)
      expect(matchesClassSpellPreset({ id, name, icon: null }, raid, 0)).toBe(true);
    expect(matchesClassSpellPreset({ id: 1, name: 'Flash Heal', icon: null }, raid, 0)).toBe(false);
    expect(
      classSpellPresets(2, 'anniversary').some((preset) => preset.key === 'RaidCooldowns')
    ).toBe(false);
  });
  it('selects role-based spell presets without calling unknown buffs damage abilities', () => {
    const spell = (id: number, name: string) => ({ id, name, icon: null });
    expect(matchesSpellPreset(spell(2825, 'Bloodlust'), 'Damage')).toBe(true);
    expect(matchesSpellPreset(spell(28507, 'Variant'), 'Consumables')).toBe(true);
    expect(matchesSpellPreset(spell(1, 'Super Mana Potion'), 'Consumables')).toBe(true);
    expect(matchesSpellPreset(spell(1, 'Healthstone'), 'Consumables')).toBe(true);
    expect(matchesSpellPreset(spell(642, 'Divine Shield'), 'Consumables')).toBe(false);
    expect(matchesSpellPreset(spell(31850, 'Ardent Defender'), 'Tank')).toBe(true);
    expect(matchesSpellPreset(spell(27137, 'Flash of Light'), 'Healing')).toBe(true);
    expect(matchesSpellPreset(spell(1, 'Unknown'), 'Damage')).toBe(false);
    expect(matchesSpellPreset(spell(1, 'Unknown'), 'Everything')).toBe(true);
  });

  it('uses class specialization icons where the role exists and generic role icons otherwise', () => {
    expect(getClassRoleIconUrl(2, 'Damage')).toBe('/images/wow/specs/paladin-retribution.jpg');
    expect(getClassRoleIconUrl(2, 'Tank')).toBe('/images/wow/specs/paladin-protection.jpg');
    expect(getClassRoleIconUrl(2, 'Healing')).toBe('/images/wow/specs/paladin-holy.jpg');
    expect(getClassRoleIconUrl(8, 'Damage')).toBe('/images/wow/specs/mage-arcane.jpg');
    expect(getClassRoleIconUrl(8, 'Tank')).toBe('/images/wow/roles/tank.jpg');
    expect(getClassRoleIconUrl(8, 'Healing')).toBe('/images/wow/roles/heal.jpg');
    expect(getClassRoleIconUrl(0, 'Damage')).toBe('/images/wow/roles/mdps.jpg');
  });

  it('hides unsupported roles and separates damage specs with shared abilities and buffs', () => {
    const mage = classSpellPresets(8, 'anniversary');
    expect(mage.map((preset) => preset.label)).toEqual([
      'Everything',
      'Arcane',
      'Fire',
      'Frost',
      'Consumables'
    ]);
    const arcane = mage.find((preset) => preset.spec === 'arcane')!;
    const frost = mage.find((preset) => preset.spec === 'frost')!;
    const spell = (name: string) => ({ id: 1, name, icon: null });
    expect(matchesClassSpellPreset(spell('Arcane Blast'), arcane, 8)).toBe(true);
    expect(matchesClassSpellPreset(spell('Arcane Blast'), frost, 8)).toBe(false);
    expect(matchesClassSpellPreset(spell('Frostbolt'), frost, 8)).toBe(true);
    expect(matchesClassSpellPreset(spell('Frostbolt'), arcane, 8)).toBe(false);
    for (const preset of mage.filter((preset) => preset.role === 'Damage')) {
      expect(matchesClassSpellPreset(spell('Mirror Image'), preset, 8)).toBe(true);
      expect(matchesClassSpellPreset(spell('Bloodlust'), preset, 8)).toBe(true);
      expect(matchesClassSpellPreset(spell('Unknown effect'), preset, 8)).toBe(false);
    }
    expect(classSpellPresets(2, 'anniversary').map((preset) => preset.label)).toEqual([
      'Everything',
      'Damage',
      'Tank',
      'Healing',
      'Consumables'
    ]);
    expect(classSpellPresets(1, 'retail').map((preset) => preset.label)).toEqual([
      'Everything',
      'Arms',
      'Fury',
      'Tank',
      'Consumables'
    ]);
    expect(classSpellPresets(4, 'anniversary').map((preset) => preset.label)).toContain('Combat');
    expect(classSpellPresets(4, 'retail').map((preset) => preset.label)).toContain('Outlaw');
  });
});
