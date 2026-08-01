export function getDisplayPercentile(value) {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return `${value.toFixed(1)}%`
  }

  return 'N/A'
}

export function getSortedReports(reports = [], sortBy = 'percentile', sortDesc = true) {
  const normalized = [...(reports || [])]

  normalized.sort((a, b) => {
    let aVal
    let bVal

    switch (sortBy) {
      case 'percentile':
        aVal = Number(a?.percentile ?? 0)
        bVal = Number(b?.percentile ?? 0)
        break
      case 'dps':
        aVal = Number(a?.players?.[0]?.performance ?? 0)
        bVal = Number(b?.players?.[0]?.performance ?? 0)
        break
      case 'duration':
        aVal = Number(a?.duration ?? 0)
        bVal = Number(b?.duration ?? 0)
        break
      default:
        return 0
    }

    aVal ??= 0
    bVal ??= 0

    if (aVal === bVal) {
      return 0
    }

    return sortDesc ? bVal - aVal : aVal - bVal
  })

  return normalized
}
