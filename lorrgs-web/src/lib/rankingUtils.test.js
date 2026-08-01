import test from 'node:test'
import assert from 'node:assert/strict'
import { getDisplayPercentile, getSortedReports } from './rankingUtils.js'

test('formats percentile values with a safe fallback', () => {
  assert.equal(getDisplayPercentile(81.234), '81.2%')
  assert.equal(getDisplayPercentile(null), 'N/A')
  assert.equal(getDisplayPercentile(undefined), 'N/A')
})

test('returns a sorted copy without mutating the original report list', () => {
  const reports = [
    { percentile: 70, players: [{ performance: 100 }] },
    { percentile: 90, players: [{ performance: 80 }] },
    { percentile: 80, players: [{ performance: 90 }] }
  ]

  const sorted = getSortedReports(reports, 'percentile', false)

  assert.deepEqual(sorted.map(report => report.percentile), [70, 80, 90])
  assert.deepEqual(reports.map(report => report.percentile), [70, 90, 80])
})
