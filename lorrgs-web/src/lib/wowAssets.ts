const WOW_ASSET_BASE = '/images/wow';

function normalizeSlug(value: string): string {
  return value
    .toLowerCase()
    .replace(/[^a-z0-9-]+/g, '-')
    .replace(/-+/g, '-')
    .replace(/^-|-$/g, '');
}

export function getClassIconUrl(classSlug: string): string {
  return `${WOW_ASSET_BASE}/classes/${normalizeSlug(classSlug)}.png`;
}

export function getSpecIconUrl(classSlug: string, specSlug: string): string {
  return `${WOW_ASSET_BASE}/specs/${normalizeSlug(classSlug)}-${normalizeSlug(specSlug)}.jpg`;
}

export function getRoleIconUrl(roleSlug: string): string {
  return `${WOW_ASSET_BASE}/roles/${normalizeSlug(roleSlug)}.jpg`;
}

const classRoleSpecs: Record<number, { classSlug: string; Damage: string[]; Tank?: string; Healing?: string }> = {
  1: { classSlug: 'warrior', Damage: ['arms', 'fury'], Tank: 'protection' },
  2: { classSlug: 'paladin', Damage: ['retribution'], Tank: 'protection', Healing: 'holy' },
  3: { classSlug: 'hunter', Damage: ['beast-mastery', 'marksmanship', 'survival'] },
  4: { classSlug: 'rogue', Damage: ['assassination', 'outlaw', 'subtlety'] },
  5: { classSlug: 'priest', Damage: ['shadow'], Healing: 'holy' },
  6: { classSlug: 'death-knight', Damage: ['frost', 'unholy'], Tank: 'blood' },
  7: { classSlug: 'shaman', Damage: ['elemental', 'enhancement'], Healing: 'restoration' },
  8: { classSlug: 'mage', Damage: ['arcane', 'fire', 'frost'] },
  9: { classSlug: 'warlock', Damage: ['affliction', 'demonology', 'destruction'] },
  10: { classSlug: 'monk', Damage: ['windwalker'], Tank: 'brewmaster', Healing: 'mistweaver' },
  11: { classSlug: 'druid', Damage: ['balance', 'feral'], Tank: 'guardian', Healing: 'restoration' },
  12: { classSlug: 'demon-hunter', Damage: ['havoc', 'devourer'], Tank: 'vengeance' },
  13: { classSlug: 'evoker', Damage: ['devastation', 'augmentation'], Healing: 'preservation' }
};

export function getClassSlugForSpec(specSlug: string | undefined): string | undefined {
  return Object.values(classRoleSpecs).find(({ classSlug }) =>
    specSlug?.endsWith(`-${classSlug}`)
  )?.classSlug;
}

export function getClassRoleIconUrl(classId: number, role: 'Damage' | 'Tank' | 'Healing'): string {
  const entry = classRoleSpecs[classId];
  const spec = role === 'Damage' ? entry?.Damage[0] : entry?.[role];
  return spec ? getSpecIconUrl(entry.classSlug, spec)
    : getRoleIconUrl(role === 'Damage' ? 'mdps' : role === 'Healing' ? 'heal' : 'tank');
}

export function getClassSpellRoles(classId: number, edition: string) {
  const entry = classRoleSpecs[classId];
  if (!entry) return [];
  const damage = entry.Damage.map((spec) =>
    spec === 'outlaw' && edition.toLowerCase() !== 'retail' ? 'combat' : spec
  );
  return [
    ...damage.map((spec) => ({
      role: 'Damage' as const,
      spec,
      label: damage.length === 1 ? 'Damage' : spec.split('-').map((part) => part[0].toUpperCase() + part.slice(1)).join(' '),
      icon: getSpecIconUrl(entry.classSlug, spec)
    })),
    ...(['Tank', 'Healing'] as const).flatMap((role) => {
      const spec = entry[role];
      return spec ? [{ role, spec, label: role, icon: getSpecIconUrl(entry.classSlug, spec) }] : [];
    })
  ];
}
