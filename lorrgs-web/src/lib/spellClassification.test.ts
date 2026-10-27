import { describe, expect, it } from 'vitest';
import { getClassRoleIconUrl } from './wowAssets';
import {
  matchesSpellPreset,
  classSpellPresets,
  matchesClassSpellPreset
} from './spellClassification';

describe('spell classification', () => {
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
