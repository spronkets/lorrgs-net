import { fireEvent, render, screen } from '@testing-library/svelte';
import { expect, it, vi } from 'vitest';
import Checkbox from './Checkbox.svelte';

it('provides an associated label and toggles the native checkbox', async () => {
  const onchange = vi.fn();
  render(Checkbox, { label: 'Spell names', onchange, name: 'spellNames' });
  const checkbox = screen.getByRole('checkbox', { name: 'Spell names' });
  expect(checkbox).not.toBeChecked();
  expect(checkbox).toHaveAttribute('name', 'spellNames');
  await fireEvent.click(screen.getByText('Spell names'));
  expect(checkbox).toBeChecked();
  expect(onchange).toHaveBeenCalledOnce();
});

it('forwards checked, disabled, custom classes, and explicit IDs', async () => {
  const { rerender } = render(Checkbox, {
    label: 'Spell names',
    id: 'spell-names',
    checked: true,
    disabled: true,
    class: 'custom'
  });
  const checkbox = screen.getByLabelText('Spell names');
  expect(checkbox).toBeChecked();
  expect(checkbox).toBeDisabled();
  expect(checkbox).toHaveClass('checkbox', 'custom');
  expect(checkbox).toHaveAttribute('id', 'spell-names');
  await fireEvent.click(screen.getByText('Spell names'));
  expect(checkbox).toBeChecked();
  await rerender({ label: 'Spell names', checked: false, disabled: false });
  expect(checkbox).not.toBeChecked();
  expect(checkbox).toBeEnabled();
});
