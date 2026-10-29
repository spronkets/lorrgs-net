import { defineConfig } from 'vitest/config';
import { svelte } from '@sveltejs/vite-plugin-svelte';

export default defineConfig({
  plugins: [svelte()],
  resolve: {
    conditions: ['browser']
  },
  test: {
    reporters: [['default', { isTTY: false }]],
    environment: 'jsdom',
    setupFiles: ['./src/setupTests.ts'],
    coverage: {
      provider: 'v8',
      reportsDirectory: './coverage',
      reporter: ['text', 'html', 'json-summary', 'json'],
      include: ['src/**/*.{ts,svelte}'],
      exclude: ['src/**/*.test.ts', 'src/setupTests.ts'],
      thresholds: {
        perFile: true,
        'src/**/*.svelte': {
          lines: 80,
          statements: 80,
          branches: 80
        }
      }
    }
  }
});
