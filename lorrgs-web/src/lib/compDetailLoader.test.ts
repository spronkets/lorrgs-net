import { expect, it } from 'vitest';
import { matchesCompExpressions } from './compDetailLoader.svelte';

it.each([
  ['heal.eq.2', true],
  ['heal.eq.1', false],
  ['heal.lt.3', true],
  ['heal.lt.2', false],
  ['heal.gt.1', true],
  ['heal.gt.2', false],
  ['heal.lte.2', true],
  ['heal.lte.1', false],
  ['heal.gte.2', true],
  ['heal.gte.3', false],
  ['heal.2', true],
  ['tank.eq.0', true],
  ['tank.gt.0', false]
])('compares inspected counts with %s', (expression, matches) => {
  expect(matchesCompExpressions({ heal: 2 }, [expression])).toBe(matches);
});

it('requires every composition expression to match', () => {
  expect(matchesCompExpressions({ heal: 2 }, ['heal.eq.2', 'tank.eq.0'])).toBe(true);
  expect(matchesCompExpressions({ heal: 2 }, ['heal.eq.2', 'tank.gt.0'])).toBe(false);
});
