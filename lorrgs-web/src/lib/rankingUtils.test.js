import { describe, expect, it } from 'vitest'
import { getDisplayPercentile, getSortedReports } from './rankingUtils.js'

describe('ranking utilities', () => {
  it('formats percentile values with a safe fallback', () => {
    expect(getDisplayPercentile(81.234)).toBe('81.2%')
    expect(getDisplayPercentile(null)).toBe('N/A')
    expect(getDisplayPercentile(undefined)).toBe('N/A')
  })

  it('returns a sorted copy without mutating the original report list', () => {
    const reports = [
      { percentile: 70, players: [{ performance: 100 }] },
      { percentile: 90, players: [{ performance: 80 }] },
      { percentile: 80, players: [{ performance: 90 }] }
    ]

    const sorted = getSortedReports(reports, 'percentile', false)

    expect(sorted.map((report) => report.percentile)).toEqual([70, 80, 90])
    expect(reports.map((report) => report.percentile)).toEqual([70, 90, 80])
  })
})
