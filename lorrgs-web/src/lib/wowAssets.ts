const WOW_ASSET_BASE = '/images/wow'

function normalizeSlug(value: string): string {
  return value.toLowerCase().replace(/[^a-z0-9]+/g, '')
}

export function getClassIconUrl(classSlug: string): string {
  return `${WOW_ASSET_BASE}/classes/${normalizeSlug(classSlug)}.png`
}

export function getSpecIconUrl(classSlug: string, specSlug: string): string {
  return `${WOW_ASSET_BASE}/specs/${normalizeSlug(classSlug)}-${normalizeSlug(specSlug)}.jpg`
}

export function getRoleIconUrl(roleSlug: string): string {
  return `${WOW_ASSET_BASE}/roles/${normalizeSlug(roleSlug)}.jpg`
}
