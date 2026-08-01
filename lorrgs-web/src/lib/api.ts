const API_BASE: string =
  (typeof import.meta !== 'undefined' && import.meta.env?.VITE_API_BASE) || '/api'
const HEALTH_READY_URL: string | undefined =
  typeof import.meta !== 'undefined' ? import.meta.env?.VITE_HEALTH_URL : undefined

type ApiOptions = {
  method?: string
  headers?: HeadersInit
}

export function buildApiUrl(endpoint: string): string {
  return `${API_BASE}${endpoint}`
}

function deriveHealthUrls(): string[] {
  if (HEALTH_READY_URL) {
    return [HEALTH_READY_URL]
  }

  const trimmedBase = API_BASE.replace(/\/+$/, '')
  const defaultUrls = ['/health']

  if (/^https?:\/\//i.test(trimmedBase)) {
    const baseWithoutApi = trimmedBase.replace(/\/api$/i, '')
    return [`${baseWithoutApi}/health`]
  }

  return defaultUrls
}

async function probeHealth(url: string): Promise<boolean> {
  try {
    const response = await fetch(url, {
      method: 'GET',
      cache: 'no-store'
    })

    return response.ok
  } catch {
    return false
  }
}

export async function waitForHealthReady(timeoutMs = 30000, intervalMs = 500): Promise<boolean> {
  const urls = deriveHealthUrls()
  const deadline = Date.now() + timeoutMs

  while (Date.now() < deadline) {
    for (const url of urls) {
      if (await probeHealth(url)) {
        return true
      }
    }

    await new Promise((resolve) => setTimeout(resolve, intervalMs))
  }

  return false
}

export function normalizeApiPayload(payload: unknown): unknown[] {
  if (Array.isArray(payload)) {
    return payload
  }

  if (payload && typeof payload === 'object') {
    return Object.values(payload as Record<string, unknown>)
  }

  return []
}

export function normalizeApiCollection(payload: unknown, fallbackKey = ''): unknown[] {
  if (Array.isArray(payload)) {
    return payload
  }

  if (payload && typeof payload === 'object') {
    const record = payload as Record<string, unknown>

    if (fallbackKey && Array.isArray(record[fallbackKey])) {
      return record[fallbackKey] as unknown[]
    }

    if (Object.values(record).every((value) => value && typeof value === 'object')) {
      return Object.values(record)
    }
  }

  return []
}

export async function get(endpoint: string, options: ApiOptions = {}): Promise<unknown> {
  const url = buildApiUrl(endpoint)
  const response = await fetch(url, {
    method: 'GET',
    headers: { 'Content-Type': 'application/json', ...options.headers },
    ...options
  })

  if (!response.ok) {
    throw new Error(`API Error: ${response.status} ${response.statusText}`)
  }

  return response.json()
}

export async function send(endpoint: string, body: unknown, options: ApiOptions = {}): Promise<unknown> {
  const url = buildApiUrl(endpoint)
  const response = await fetch(url, {
    method: options.method || 'POST',
    headers: { 'Content-Type': 'application/json', ...options.headers },
    body: JSON.stringify(body ?? {}),
    ...options
  })

  if (!response.ok) {
    throw new Error(`API Error: ${response.status} ${response.statusText}`)
  }

  return response.json()
}

// World Data endpoints
export async function getSeasons() {
  try {
    const payload = await get('/worlddata/seasons')
    return normalizeApiCollection(payload, 'seasons')
  } catch {
    return []
  }
}

export async function getSeason(slug: string) {
  return get(`/worlddata/seasons/${slug}`)
}

export async function getClasses() {
  const payload = await get('/worlddata/classes')
  return normalizeApiCollection(payload, 'classes')
}

export async function getSpecs() {
  const payload = await get('/worlddata/specs')
  return normalizeApiCollection(payload, 'specs')
}

export async function getRoles() {
  const payload = await get('/worlddata/roles')
  return normalizeApiCollection(payload, 'roles')
}

export async function getZones() {
  const payload = await get('/worlddata/zones')
  return normalizeApiCollection(payload, 'zones')
}

export async function getZone(slug: string) {
  return get(`/worlddata/zones/${slug}`)
}

export async function getZoneBosses(zoneSlug: string) {
  const payload = await get(`/worlddata/zones/${zoneSlug}/bosses`)
  return normalizeApiCollection(payload, 'bosses')
}

export async function getBosses() {
  const payload = await get('/worlddata/bosses')
  return normalizeApiCollection(payload, 'bosses')
}

export async function getBoss(slug: string) {
  return get(`/worlddata/bosses/${slug}`)
}

export async function getSpells() {
  const payload = await get('/worlddata/spells')
  return normalizeApiCollection(payload, 'spells')
}

export async function getTrinkets() {
  const payload = await get('/worlddata/trinkets')
  return normalizeApiCollection(payload, 'trinkets')
}

export async function getTrinket(id: string | number) {
  return get(`/worlddata/trinkets/${id}`)
}

export async function getRaidCatalog() {
  return get('/raid-catalog')
}

// Rankings endpoints
export async function getSpecRankings(specSlug: string, bossSlug: string, difficulty = 'Mythic', metric = 'dps') {
  const normalizedDifficulty = String(difficulty || 'mythic').trim().toLowerCase()
  const normalizedMetric = String(metric || 'dps').trim().toLowerCase()
  const params = new URLSearchParams({ difficulty: normalizedDifficulty, metric: normalizedMetric })
  return get(`/rankings/spec/${specSlug}/${bossSlug}?${params}`)
}

export async function getSpecRankingsInfo(specSlug: string, bossSlug: string) {
  return get(`/rankings/spec/${specSlug}/${bossSlug}/info`)
}

export async function getCompRankings(bossSlug: string, limit = 25, roles: string[] = [], specs: string[] = []) {
  const params = new URLSearchParams({ limit: String(limit) })
  if (roles.length) roles.forEach((r) => params.append('role', r))
  if (specs.length) specs.forEach((s) => params.append('spec', s))
  return get(`/rankings/comp/${bossSlug}?${params}`)
}

export async function queueSpecRankingUpdate(
  specSlug: string,
  bossSlug: string,
  difficulty: string,
  metric: string,
  limit = 25
) {
  const params = new URLSearchParams({ specSlug, bossSlug, difficulty, metric, limit: String(limit) })
  return get(`/rankings/spec/queue?${params}`, { method: 'POST' })
}

export async function queueCompRankingUpdate(bossSlug: string, limit = 25) {
  const params = new URLSearchParams({ bossSlug, limit: String(limit) })
  return get(`/rankings/comp/queue?${params}`, { method: 'POST' })
}

export async function markRankingDirty(specSlug: string, bossSlug: string, difficulty: string, metric: string) {
  const params = new URLSearchParams({ specSlug, bossSlug, difficulty, metric })
  return get(`/rankings/spec/dirty?${params}`, { method: 'PATCH' })
}

// Rotation analysis endpoints
export async function lookupReport(reportCode: string) {
  return get(`/warcraftlogs/reports/${reportCode}/lookup`)
}

export async function analyzeRotation(payload: unknown) {
  return send('/warcraftlogs/rotation/analyze', payload, { method: 'POST' })
}