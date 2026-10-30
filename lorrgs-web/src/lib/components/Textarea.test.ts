import { fireEvent, render, screen } from '@testing-library/svelte';
import { expect, it, vi } from 'vitest';
import Textarea from './Textarea.svelte';

it('forwards native attributes and input events and updates the value', async () => {
  const oninput = vi.fn();
  render(Textarea, {
    'aria-label': 'Reports', value: 'ReportA', rows: 3,
    placeholder: 'Enter codes', name: 'reports', class: 'custom', oninput
  });
  const field = screen.getByRole('textbox');
  expect(field).toHaveValue('ReportA');
  expect(field).toHaveAttribute('rows', '3');
  expect(field).toHaveAttribute('name', 'reports');
  expect(field).toHaveAttribute('placeholder', 'Enter codes');
  expect(field).toHaveClass('textarea', 'custom');
  await fireEvent.input(field, { target: { value: 'ReportB' } });
  expect(field).toHaveValue('ReportB');
  expect(oninput).toHaveBeenCalledOnce();
});

it('supports an empty disabled textarea', () => {
  render(Textarea, { disabled: true, 'aria-label': 'Reports' });
  expect(screen.getByRole('textbox')).toHaveValue('');
  expect(screen.getByRole('textbox')).toBeDisabled();
});

it('supports a monospace preview without forwarding the component option as an attribute', () => {
  render(Textarea, { label: 'Preview', monospace: true, style: 'min-height: 260px' });
  const field = screen.getByRole('textbox');
  expect(field).toHaveClass('monospace');
  expect(field).not.toHaveAttribute('monospace');
  expect(field).toHaveStyle('min-height: 260px');
});

it('associates its visible label with the textarea using an automatically generated ID', () => {
  render(Textarea, { label: 'Report codes or URLs' });
  const field = screen.getByLabelText('Report codes or URLs');
  expect(field.id).not.toBe('');
  expect(field).toBe(screen.getByRole('textbox'));
});
