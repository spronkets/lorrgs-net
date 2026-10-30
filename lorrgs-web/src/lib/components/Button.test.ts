import { fireEvent, render, screen } from '@testing-library/svelte';
import { createRawSnippet } from 'svelte';
import { expect, it, vi } from 'vitest';
import Button from './Button.svelte';

const children = createRawSnippet(() => ({ render: () => '<span>Action</span>' }));

it('uses solid primary when pressed and secondary-outline when unpressed', async () => {
  const { rerender } = render(Button, {
    children,
    variant: 'secondary-outline',
    'aria-pressed': true
  });
  const button = screen.getByRole('button');
  expect(button).toHaveAttribute('data-tone', 'primary');
  expect(button).toHaveAttribute('data-outline', 'false');
  await rerender({ children, variant: 'primary-outline', 'aria-pressed': false });
  expect(button).toHaveAttribute('data-tone', 'secondary');
  expect(button).toHaveAttribute('data-outline', 'true');
});

it('defaults to a primary non-submitting button and forwards native attributes and events', async () => {
  const onclick = vi.fn();
  render(Button, {
    children,
    onclick,
    class: 'custom',
    'aria-label': 'Run action',
    name: 'action',
    value: 'run'
  });
  const button = screen.getByRole('button', { name: 'Run action' });
  expect(button).toHaveAttribute('type', 'button');
  expect(button).toHaveAttribute('data-tone', 'primary');
  expect(button).toHaveAttribute('data-outline', 'false');
  expect(button).toHaveAttribute('data-size', 'medium');
  expect(button).toHaveAttribute('name', 'action');
  expect(button).toHaveAttribute('value', 'run');
  expect(button).toHaveClass('button', 'custom');
  await fireEvent.click(button);
  expect(onclick).toHaveBeenCalledOnce();
});

it.each([
  'primary',
  'primary-outline',
  'secondary',
  'secondary-outline',
  'success',
  'success-outline',
  'warning',
  'warning-outline',
  'danger',
  'danger-outline',
  'unstyled'
] as const)('supports the %s variant', (variant) => {
  render(Button, { children, variant });
  const button = screen.getByRole('button');
  expect(button).toHaveAttribute('data-tone', variant.replace('-outline', ''));
  expect(button).toHaveAttribute('data-outline', String(variant.endsWith('-outline')));
});

it('supports conditional classes for custom controls', () => {
  render(Button, {
    children,
    variant: 'unstyled',
    class: ['event', { active: true, muted: false }]
  });
  expect(screen.getByRole('button')).toHaveClass('button', 'event', 'active');
  expect(screen.getByRole('button')).not.toHaveClass('muted');
});

it.each(['small', 'medium', 'large'] as const)(
  'supports %s sizing and disabled submit buttons',
  (size) => {
    const onclick = vi.fn();
    render(Button, { children, size, disabled: true, type: 'submit', onclick });
    const button = screen.getByRole('button');
    expect(button).toBeDisabled();
    expect(button).toHaveAttribute('type', 'submit');
    expect(button).toHaveAttribute('data-size', size);
    button.click();
    expect(onclick).not.toHaveBeenCalled();
  }
);
