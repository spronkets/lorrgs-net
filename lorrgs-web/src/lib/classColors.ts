const BLIZZARD_CLASS_COLORS: Record<string, string> = {
  'death-knight': '#C41E3A',
  'demon-hunter': '#A330C9',
  druid: '#FF7C0A',
  evoker: '#33937F',
  hunter: '#AAD372',
  mage: '#3FC7EB',
  monk: '#00FF98',
  paladin: '#F48CBA',
  priest: '#FFFFFF',
  rogue: '#FFF468',
  shaman: '#0070DD',
  warlock: '#8788EE',
  warrior: '#C69B6D'
};

export function getBlizzardClassColor(
  classSlug: string | undefined | null,
  fallback = '#7BA4BF'
): string {
  if (!classSlug) {
    return fallback;
  }

  return BLIZZARD_CLASS_COLORS[classSlug] || fallback;
}

export function getBlizzardClassColors(): Record<string, string> {
  return BLIZZARD_CLASS_COLORS;
}
