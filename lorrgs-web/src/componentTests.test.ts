import { expect, it } from 'vitest';

it('keeps an accompanying test file beside every Svelte component', () => {
  const components = Object.keys(import.meta.glob('./**/*.svelte'));
  const tests = import.meta.glob('./**/*.test.ts');
  const missing = components.filter(
    (component) => !(component.replace(/\.svelte$/, '.test.ts') in tests)
  );
  expect(missing, 'Components missing accompanying functional tests').toEqual([]);
});

it('uses the shared Button component for every native button', () => {
  const components = import.meta.glob<string>('./**/*.svelte', {
    query: '?raw',
    import: 'default',
    eager: true
  });
  const nativeButtons = Object.entries(components)
    .filter(([file, source]) => !file.endsWith('/Button.svelte') && /<button\b/.test(source))
    .map(([file]) => file);
  expect(nativeButtons, 'Native buttons must be rendered through Button.svelte').toEqual([]);
});
