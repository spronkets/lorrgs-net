import js from '@eslint/js';
import globals from 'globals';
import svelte from 'eslint-plugin-svelte';
import tsParser from '@typescript-eslint/parser';

export default [
  {
    ignores: ['dist/**', 'node_modules/**']
  },
  js.configs.recommended,
  ...svelte.configs['flat/recommended'],
  {
    files: ['**/*.svelte.ts'],
    languageOptions: {
      parser: tsParser,
      globals: { ...globals.browser, $state: 'readonly' }
    },
    rules: { 'no-unused-vars': ['error', { args: 'none' }] }
  },
  {
    files: ['**/*.{js,mjs,cjs}'],
    languageOptions: {
      sourceType: 'module',
      globals: {
        ...globals.browser,
        ...globals.node
      }
    }
  },
  {
    files: ['**/*.svelte'],
    languageOptions: {
      parserOptions: {
        parser: tsParser
      },
      globals: {
        ...globals.browser
      }
    },
    rules: {
      'no-unused-vars': [
        'error',
        {
          argsIgnorePattern: '^_',
          varsIgnorePattern: '^_',
          caughtErrorsIgnorePattern: '^_'
        }
      ]
    }
  }
];
