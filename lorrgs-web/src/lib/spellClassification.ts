import type { TimelineSpell } from './api';
import { getClassSpellRoles } from './wowAssets';
import { isPowerWindow, spellIconUrl } from './timelineUtils';

export const spellPresets = ['Everything', 'Damage', 'Tank', 'Healing', 'Consumables'] as const;
export type SpellPreset = (typeof spellPresets)[number];

export type ClassSpellPreset = {
  key: string;
  label: string;
  role: SpellPreset;
  spec?: string;
  icon: string;
};

export function classSpellPresets(classId: number, edition: string): ClassSpellPreset[] {
  return [
    {
      key: 'Everything',
      label: 'Everything',
      role: 'Everything',
      icon: spellIconUrl('inv_misc_book_09')!
    },
    ...getClassSpellRoles(classId, edition).map((entry) => ({
      ...entry,
      key: `${entry.role}:${entry.spec}`
    })),
    {
      key: 'Consumables',
      label: 'Consumables',
      role: 'Consumables',
      icon: spellIconUrl('inv_potion_54')!
    }
  ];
}

const damageSpecNames: Record<string, RegExp> = {
  arms: /mortal strike|overpower|bladestorm|colossus smash|warbreaker|sweeping strikes/i,
  fury: /bloodthirst|rampage|raging blow|recklessness|death wish|enraged regeneration/i,
  'beast-mastery': /bestial wrath|beast cleave|barbed shot|kill command|dire beast/i,
  marksmanship: /aimed shot|rapid fire|trueshot|chimera shot|readiness|steady shot/i,
  survival: /explosive shot|black arrow|wildfire bomb|mongoose bite|coordinated assault/i,
  assassination: /mutilate|envenom|vendetta|deathmark|kingsbane|shiv/i,
  outlaw:
    /sinister strike|pistol shot|dispatch|adrenaline rush|killing spree|roll the bones|between the eyes/i,
  combat: /sinister strike|adrenaline rush|killing spree|blade flurry/i,
  subtlety:
    /backstab|shadowstrike|shadow dance|symbols of death|secret technique|gloomblade|shadow blades|hemorrhage/i,
  shadow: /mind blast|mind flay|shadow word|vampiric touch|void|shadowfiend|dark ascension/i,
  frost:
    /frost|ice lance|icy veins|blizzard|cold snap|frozen orb|howling blast|obliterate|pillar of frost|breath of sindragosa|remorseless winter|glacial spike/i,
  unholy:
    /festering|scourge strike|apocalypse|dark transformation|army of the dead|summon gargoyle|unholy|death coil/i,
  elemental: /lava burst|earth shock|earthquake|elemental mastery|stormkeeper|ascendance/i,
  enhancement: /stormstrike|lava lash|feral spirit|doom winds|windfury|sundering|crash lightning/i,
  arcane: /arcane|presence of mind|evocation|touch of the magi/i,
  fire: /fireball|fire blast|pyroblast|combustion|scorch|flamestrike|dragon's breath|phoenix flames|living bomb|blast wave/i,
  affliction:
    /unstable affliction|haunt|agony|corruption|drain soul|darkglare|malefic rapture|siphon life/i,
  demonology:
    /hand of gul'dan|demonbolt|demonic|metamorphosis|summon felguard|implosion|call dreadstalkers|soul strike/i,
  destruction:
    /chaos bolt|conflagrate|incinerate|immolate|rain of fire|shadowburn|summon infernal|havoc/i,
  balance:
    /starfire|wrath|starsurge|starfall|moonfire|sunfire|celestial alignment|incarnation: chosen of elune|force of nature/i,
  feral:
    /shred|rake|rip|ferocious bite|tiger's fury|berserk|incarnation: avatar of ashamane|mangle/i,
  havoc: /eye beam|blade dance|death sweep|chaos strike|annihilation|essence break|metamorphosis/i,
  devourer: /void ray|collapsing star|void metamorphosis|reap|devour/i,
  devastation: /disintegrate|pyre|dragonrage|fire breath|eternity surge|deep breath|firestorm/i,
  augmentation: /ebon might|eruption|upheaval|prescience|breath of eons|blistering scales/i
};
const sharedDamageNames: Record<number, RegExp> = {
  1: /^(execute|heroic strike|cleave|whirlwind|slam|battle shout|bloodrage|berserker rage)$/i,
  3: /^(auto shot|arcane shot|multi-shot|kill shot|serpent sting|aspect of the hawk|rapid killing)$/i,
  4: /^(eviscerate|rupture|slice and dice|garrote|ambush|kick|expose armor)$/i,
  6: /^(death strike|blood strike|plague strike|icy touch|horn of winter|empower rune weapon)$/i,
  7: /^(lightning bolt|chain lightning|flame shock|frost shock|bloodlust|heroism|fire elemental totem)$/i,
  8: /^(frostfire bolt|mirror image|counterspell|spellsteal|shoot)$/i,
  9: /^(shadow bolt|soul fire|life tap|drain life|seed of corruption|curse of elements|curse of doom|curse of agony)$/i,
  11: /^(faerie fire|faerie fire \(feral\)|convoke the spirits|moonfire)$/i,
  12: /^(felblade|throw glaive|immolation aura|the hunt)$/i,
  13: /^(living flame|azure strike|hover|tip the scales|fury of the aspects)$/i
};

export function matchesClassSpellPreset(
  spell: TimelineSpell,
  preset: ClassSpellPreset,
  classId: number
): boolean {
  if (preset.role !== 'Damage' || !preset.spec || !damageSpecNames[preset.spec])
    return matchesSpellPreset(spell, preset.role);
  return (
    isPowerWindow(spell) ||
    Boolean(sharedDamageNames[classId]?.test(spell.name)) ||
    damageSpecNames[preset.spec].test(spell.name)
  );
}

// Upstream SpellTag IDs, including spell variations. Unknown effects remain in Everything.
const presetIds: Record<Exclude<SpellPreset, 'Everything'>, ReadonlySet<number>> = {
  Damage: new Set([
    1122, 1719, 12051, 13750, 42650, 51690, 102543, 106951, 114050, 114051, 121471, 123904, 190319,
    200166, 205180, 228260, 265187, 279302, 288613, 321507, 360194, 368847, 375087, 375576, 385059,
    385627, 403631, 433874, 442204, 446035
  ]),
  Tank: new Set([
    871, 1160, 31850, 49028, 50334, 55233, 61336, 86659, 102558, 115176, 123981, 132578, 187827,
    204021, 209261, 212641
  ]),
  Healing: new Set([
    740, 15286, 31821, 31884, 33891, 51052, 62618, 64843, 97462, 98008, 108280, 114052, 115310,
    196718, 200183, 216331, 231895, 322118, 325197, 359816, 363534, 370537, 370960, 374227, 374968,
    388615, 389539, 391528, 421453, 472433
  ]),
  Consumables: new Set([
    17528, 17530, 17531, 17534, 17535, 22734, 28507, 28508, 28509, 28515, 28518, 28520, 28521,
    28540, 28555, 28556, 28557, 28558, 28565, 28579, 28595, 31666, 53746, 53748, 53908, 53909,
    53910, 53911, 53914, 53915
  ])
};
const presetNames: Record<Exclude<SpellPreset, 'Everything'>, RegExp> = {
  Damage:
    /^(crusader strike|judg(e)?ment|consecration|exorcism|hammer of wrath|seal of (blood|command|righteousness|vengeance)|holy wrath|divine storm|templar's verdict|wake of ashes|execution sentence)$/i,
  Tank: /^(holy shield|shield of the righteous|righteous defense|righteous fury|ardent defender|guardian of ancient kings|shield block|shield wall|last stand)$/i,
  Healing:
    /^(holy light|flash of light|holy shock|lay on hands|light of dawn|word of glory|renew|rejuvenation|regrowth|healing touch|chain heal|healing wave|lesser healing wave|prayer of healing|binding heal|flash heal|greater heal)$/i,
  Consumables:
    /\b(potion|elixir|flask|healthstone|healing stone|mana gem|drums|food|well fed)\b|^(haste|insane strength|super mana|destruction|ironshield)$/i
};

export function matchesSpellPreset(spell: TimelineSpell, preset: SpellPreset): boolean {
  return (
    preset === 'Everything' ||
    presetIds[preset].has(spell.id) ||
    presetNames[preset].test(spell.name) ||
    (preset === 'Damage' && isPowerWindow(spell))
  );
}
