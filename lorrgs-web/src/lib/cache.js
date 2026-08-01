// localStorage cache with TTL support
const TTL = {
  WORLD_DATA: 24 * 60 * 60 * 1000, // 24 hours
  RANKINGS: 5 * 60 * 1000 // 5 minutes
}

function getCached(prefix, key) {
  const cacheKey = `${prefix}:${key}`
  const cached = localStorage.getItem(cacheKey)

  if (!cached) return null

  try {
    const { data, expires } = JSON.parse(cached)

    if (Date.now() < expires) {
      return data
    }

    // Expired, remove it
    localStorage.removeItem(cacheKey)
    return null
  } catch (err) {
    console.error('Cache parse error:', err)
    localStorage.removeItem(cacheKey)
    return null
  }
}

function setCached(prefix, key, data, ttl) {
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

function clearCachePrefix(prefix) {
  const keysToDelete = []
  for (let i = 0; i < localStorage.length; i++) {
    const key = localStorage.key(i)
    if (key?.startsWith(prefix + ':')) {
      keysToDelete.push(key)
    }
  }
  keysToDelete.forEach((key) => localStorage.removeItem(key))
}

// World data caching (24 hour TTL)
export function cacheWorldData(key, data) {
  setCached('worldData', key, data, TTL.WORLD_DATA)
}

export function getWorldDataCache(key) {
  return getCached('worldData', key)
}

// Rankings caching (5 minute TTL)
export function cacheRankings(key, data) {
  setCached('rankings', key, data, TTL.RANKINGS)
}

export function getRankingsCache(key) {
  return getCached('rankings', key)
}

export function clearRankingsCache() {
  clearCachePrefix('rankings')
}

export function clearAllCache() {
  clearCachePrefix('worldData')
  clearCachePrefix('rankings')
}
