type CacheEntry<T> = {
  data: T
  expires: number
}

const TTL = {
  WORLD_DATA: 24 * 60 * 60 * 1000,
  RANKINGS: 5 * 60 * 1000
} as const

function getCached<T>(prefix: string, key: string): T | null {
  const cacheKey = `${prefix}:${key}`
  const cached = localStorage.getItem(cacheKey)

  if (!cached) return null

  try {
    const { data, expires } = JSON.parse(cached) as CacheEntry<T>

    if (Date.now() < expires) {
      return data
    }

    localStorage.removeItem(cacheKey)
    return null
  } catch (err) {
    console.error('Cache parse error:', err)
    localStorage.removeItem(cacheKey)
    return null
  }
}

function setCached<T>(prefix: string, key: string, data: T, ttl: number): void {
  const cacheKey = `${prefix}:${key}`

  try {
    localStorage.setItem(
      cacheKey,
      JSON.stringify({
        data,
        expires: Date.now() + ttl
      })
    )
  } catch (err) {
    console.error('Cache write error:', err)
  }
}

function clearCachePrefix(prefix: string): void {
  const keysToDelete: string[] = []
  for (let i = 0; i < localStorage.length; i++) {
    const key = localStorage.key(i)
    if (key?.startsWith(prefix + ':')) {
      keysToDelete.push(key)
    }
  }
  keysToDelete.forEach((key) => localStorage.removeItem(key))
}

export function cacheWorldData<T>(key: string, data: T): void {
  setCached('worldData', key, data, TTL.WORLD_DATA)
}

export function getWorldDataCache<T>(key: string): T | null {
  return getCached<T>('worldData', key)
}

export function cacheRankings<T>(key: string, data: T): void {
  setCached('rankings', key, data, TTL.RANKINGS)
}

export function getRankingsCache<T>(key: string): T | null {
  return getCached<T>('rankings', key)
}

export function clearRankingsCache(): void {
  clearCachePrefix('rankings')
}

export function clearAllCache(): void {
  clearCachePrefix('worldData')
  clearCachePrefix('rankings')
}
