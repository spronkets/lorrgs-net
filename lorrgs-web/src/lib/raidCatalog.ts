export type RaidEdition = {
  id: number
  slug: string
  name: string
  order: number
  public: boolean
}

export type RaidInstance = {
  id: number
  slug: string
  name: string
  edition: string
  phase: number
  slots: number
  normalAndHeroic: boolean
  bossOrder: string[]
  bosses?: RaidBossOption[]
  public: boolean
}

export type RaidPhaseGroup = {
  key: string
  label: string
  phase: number | null
  raids: RaidInstance[]
}

export type RaidCatalog = {
  editions: RaidEdition[]
  instances: Record<string, RaidInstance[]>
}

export type RaidBossOption = {
  name: string
  slug: string
  mapped: boolean
}

export type WowClassLike = {
  id: number
  name?: string
  nameSlug?: string
}

export type WowSpecLike = {
  id: number
  name?: string
  nameSlug?: string
  fullName?: string
  fullNameSlug?: string
  classId: number
  roleId?: number
}

const versionToEditionSlug: Record<string, string> = {
  Anniversary: 'tbc',
  Era: 'classic',
  Retail: 'wotlk',
  'Mists of Pandaria': 'mop'
}

const versionToClassIds: Record<string, number[]> = {
  Anniversary: [1, 2, 3, 4, 5, 7, 8, 9, 11],
  Era: [1, 2, 3, 4, 5, 7, 8, 9, 11],
  Retail: [1, 2, 3, 4, 5, 6, 7, 8, 9, 11],
  'Mists of Pandaria': [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11]
}

export function getEditionSlug(version: string): string {
  return versionToEditionSlug[version] || ''
}

export function getRaidsForVersion(catalog: RaidCatalog | null | undefined, version: string): RaidInstance[] {
  const editionSlug = getEditionSlug(version)
  if (!editionSlug) {
    return []
  }

  return catalog?.instances?.[editionSlug] || []
}

export function getRaidBossOptions(
  raid: RaidInstance | null | undefined,
): RaidBossOption[] {
  if (!raid) {
    return []
  }

  if (Array.isArray(raid.bosses) && raid.bosses.length) {
    return raid.bosses
  }

  return (raid.bossOrder || []).map((name) => ({
    name,
    slug: '',
    mapped: false
  }))
}

export function groupRaidsByPhase(raids: RaidInstance[] | null | undefined): RaidPhaseGroup[] {
  type RaidGrouping = {
    key: string
    label: string
    phase: number | null
    sortOrder: number
    raids: RaidInstance[]
  }

  const grouped = new Map<string, RaidGrouping>()

  const knownWorldBossSlugs = new Set([
    'azuregos',
    'lordkazzak',
    'doomwalker',
    'doomlordkazzak',
    'dragonsofnightmare',
    'azuregossod',
    'lordkazzaksod',
    'dragonsofnightmaresod',
    'crystalvale'
  ])

  const isWorldBossRaid = (raid: RaidInstance): boolean => {
    const slug = (raid.slug || '').toLowerCase()
    const name = (raid.name || '').toLowerCase()
    return knownWorldBossSlugs.has(slug) || slug.includes('world-boss') || slug.includes('worldboss') || name.includes('world boss')
  }

  for (const raid of raids || []) {
    const phase = raid.phase || 0
    const worldBoss = isWorldBossRaid(raid)

    const fallbackPhase = worldBoss ? null : (phase > 0 ? phase : 999)
    const key = worldBoss ? 'world-bosses' : `phase-${fallbackPhase}`
    const label = worldBoss ? 'World Bosses' : `Phase ${fallbackPhase}`
    const phaseValue = worldBoss ? null : fallbackPhase
    const sortOrder = worldBoss ? Number.MAX_SAFE_INTEGER : fallbackPhase

    if (!grouped.has(key)) {
      grouped.set(key, {
        key,
        label,
        phase: phaseValue,
        sortOrder,
        raids: []
      })
    }

    grouped.get(key)?.raids.push(raid)
  }

  return Array.from(grouped.values())
    .sort((left, right) => left.sortOrder - right.sortOrder || left.label.localeCompare(right.label))
    .map((group) => ({
      key: group.key,
      label: group.label,
      phase: group.phase,
      raids: group.raids.slice().sort((left, right) => left.name.localeCompare(right.name))
    }))
}

export function getAllowedClassIds(version: string): number[] {
  return versionToClassIds[version] || []
}

export function getAvailableClassesForVersion(classes: WowClassLike[] | null | undefined, version: string): WowClassLike[] {
  const allowedClassIds = new Set(getAllowedClassIds(version))
  if (!allowedClassIds.size) {
    return classes || []
  }

  return (classes || []).filter((cls) => allowedClassIds.has(cls.id))
}

export function getAvailableSpecsForVersion(specs: WowSpecLike[] | null | undefined, version: string): WowSpecLike[] {
  const allowedClassIds = new Set(getAllowedClassIds(version))
  if (!allowedClassIds.size) {
    return specs || []
  }

  return (specs || []).filter((spec) => allowedClassIds.has(spec.classId))
}
