const WOWHEAD_ICON_CDN = 'https://wow.zamimg.com/images/wow/icons/large'

const versionIconByName: Record<string, string> = {
  'Anniversary': '/images/wow/editions/burning-crusade-classic.png',
  'Mists of Pandaria': '/images/wow/editions/mists-of-pandaria-classic.png',
  'Era': '/images/wow/editions/classic.png',
  'Retail': '/images/wow/editions/midnight.png',
}

function normalizeIconName(iconName: string): string {
  return iconName.replace(/^\/+/, '').replace(/\.[^.]+$/, '')
}

export function getWowheadCdnIconUrl(iconName: string | undefined | null): string {
  if (!iconName) {
    return `${WOWHEAD_ICON_CDN}/inv_misc_questionmark.jpg`
  }

  return `${WOWHEAD_ICON_CDN}/${normalizeIconName(iconName)}.jpg`
}

export function getVersionIconUrl(version: string): string {
  return versionIconByName[version] || '/images/wow/editions/classic.png'
}

export function getZoneIconUrl(iconName: string | undefined | null): string {
  return getWowheadCdnIconUrl(iconName)
}
