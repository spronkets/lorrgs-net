import { render, screen } from '@testing-library/svelte';
import { createRawSnippet } from 'svelte';
import { expect, it } from 'vitest';
import FormField from './FormField.svelte';

const children = createRawSnippet(() => ({ render: () => '<input id="field" />' }));

it('provides a visible label associated with its child control', () => {
  render(FormField, { id: 'field', label: 'Report', children });
  expect(screen.getByLabelText('Report')).toBe(screen.getByRole('textbox'));
});

it('does not invent a label for an unlabeled control', () => {
  const { container } = render(FormField, { id: 'field', children });
  expect(container.querySelector('label')).toBeNull();
  expect(screen.getByRole('textbox')).toBeInTheDocument();
});
