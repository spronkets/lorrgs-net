import test from 'node:test'
import assert from 'node:assert/strict'
import { buildApiUrl, normalizeApiCollection, normalizeApiPayload } from './api'

test('builds URLs relative to the configured API base', () => {
  const url = buildApiUrl('/specs')
  assert.match(url, /\/specs$/)
})

test('normalizes object payloads into arrays for the UI', () => {
  const payload = {
    first: { id: 1, name: 'Alpha' },
    second: { id: 2, name: 'Beta' }
  }

  assert.deepEqual(normalizeApiPayload(payload), [
    { id: 1, name: 'Alpha' },
    { id: 2, name: 'Beta' }
  ])
})

test('normalizes collection payloads from either object maps or wrapped arrays', () => {
  assert.deepEqual(normalizeApiCollection({ classes: [{ id: 1 }] }, 'classes'), [{ id: 1 }])
  assert.deepEqual(normalizeApiCollection({ first: { id: 1 }, second: { id: 2 } }), [{ id: 1 }, { id: 2 }])
})
