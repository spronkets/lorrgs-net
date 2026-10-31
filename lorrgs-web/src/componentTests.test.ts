import { expect, it } from 'vitest';

it('keeps an accompanying test file beside every Svelte component', () => {
  const components = Object.keys(import.meta.glob('./**/*.svelte'));
  const tests = import.meta.glob('./**/*.test.ts');
  const missing = components.filter(
    (component) => !(component.replace(/\.svelte$/, '.test.ts') in tests)
  );
  expect(missing, 'Components missing accompanying functional tests').toEqual([]);
});
