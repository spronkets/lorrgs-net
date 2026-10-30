const WOWHEAD_ICON_CDN = 'https://wow.zamimg.com/images/wow/icons';

const versionIconByName: Record<string, string> = {
  Anniversary: '/images/wow/editions/anniversary.png',
  Forever: '/images/wow/editions/forever.png',
  'Mists of Pandaria': '/images/wow/editions/mop-classic.png',
  Era: '/images/wow/editions/era.png',
  'Season of Discovery': '/images/wow/editions/sod-classic.png',
  Retail: '/images/wow/editions/retail.png'
};

function normalizeIconName(iconName: string): string {
  return iconName.replace(/^\/+/, '').replace(/\.[^.]+$/, '');
}

export function getWowheadCdnIconUrl(
  iconName: string | undefined | null,
  size: 'medium' | 'large' = 'large'
): string {
  if (!iconName) {
    return `${WOWHEAD_ICON_CDN}/${size}/inv_misc_questionmark.jpg`;
  }

  return `${WOWHEAD_ICON_CDN}/${size}/${normalizeIconName(iconName)}.jpg`;
}

export function getVersionIconUrl(version: string): string {
  return versionIconByName[version] || '/images/wow/editions/classic.png';
}

export function getZoneIconUrl(iconName: string | undefined | null): string {
  return getWowheadCdnIconUrl(iconName);
}
