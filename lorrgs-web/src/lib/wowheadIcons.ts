const WOWHEAD_ICON_BASE = '/images/wow'

function normalizeIconName(iconName: string): string {
  return iconName.replace(/^\/+/, '').replace(/\.[^.]+$/, '')
}

export function getWowheadIconUrl(
  iconName: string | undefined | null,
  category: 'bosses' | 'spells' | 'trinkets' | 'shared' = 'bosses'
): string {
  if (!iconName) {
    return `${WOWHEAD_ICON_BASE}/shared/inv_misc_questionmark.jpg`
  }

  return `${WOWHEAD_ICON_BASE}/${category}/${normalizeIconName(iconName)}.jpg`
}
