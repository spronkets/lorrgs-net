import { fireEvent, render, screen } from '@testing-library/svelte';
import { expect, it, vi } from 'vitest';
import Input from './Input.svelte';
import { createRawSnippet } from 'svelte';

it('groups prefix and suffix affixes around the editable input', async () => {
  const prefix = createRawSnippet(() => ({ render: () => '<span>Icon</span>' }));
  const suffix = createRawSnippet(() => ({ render: () => '<select aria-label="Comparison"><option>=</option></select>' }));
  const { container } = render(Input, { prefix, suffix, 'aria-label': 'Count', placeholder: 'Any' });
  const frame = container.querySelector('.affixed')!;
  expect(frame.querySelector('.prefix')).toHaveTextContent('Icon');
  expect(frame.querySelector('input')).toHaveAttribute('placeholder', 'Any');
  expect(frame.querySelector('.suffix select')).toBe(screen.getByRole('combobox'));
  await fireEvent.input(screen.getByRole('textbox'), { target: { value: '12' } });
  expect(screen.getByRole('textbox')).toHaveValue('12');
});

it('associates a generated label and forwards native input attributes and events', async () => {
  const oninput = vi.fn();
  render(Input, { label: 'Report code', value: 'ReportA', placeholder: 'Code', name: 'report', oninput });
  const input = screen.getByRole('textbox', { name: 'Report code' });
  expect(screen.getByLabelText('Report code')).toBe(input);
  expect(input.id).not.toBe('');
  expect(input).toHaveValue('ReportA');
  expect(input).toHaveAttribute('name', 'report');
  await fireEvent.input(input, { target: { value: 'ReportB' } });
  expect(input).toHaveValue('ReportB');
  expect(oninput).toHaveBeenCalledOnce();
});

it('supports explicit IDs, numeric controls, compact layout and action cleanup', () => {
  const destroy = vi.fn();
  const action = vi.fn(() => ({ destroy }));
  const { unmount } = render(Input, {
    label: 'Count', id: 'count', type: 'number', value: 2, min: 0, compact: true, action
  });
  const input = screen.getByLabelText('Count');
  expect(input).toHaveAttribute('id', 'count');
  expect(input).toHaveValue(2);
  expect(input).toHaveClass('compact');
  expect(action).toHaveBeenCalledWith(input);
  unmount();
  expect(destroy).toHaveBeenCalledOnce();
});

it('supports unlabeled controls with accessible names and disabled state', () => {
  render(Input, { 'aria-label': 'Search', disabled: true });
  expect(screen.getByRole('textbox', { name: 'Search' })).toBeDisabled();
  expect(screen.getByRole('textbox')).toHaveValue('');
});
