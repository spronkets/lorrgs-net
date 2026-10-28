import { render, screen } from '@testing-library/svelte';
import { createRawSnippet } from 'svelte';
import { expect, it } from 'vitest';
import Alert from './Alert.svelte';

const children = createRawSnippet(() => ({
  render: () => '<span>Warcraft Logs needs a breather.</span>'
}));

it.each([
  ['error', 'alert'],
  ['warning', 'status'],
  ['info', 'status']
] as const)('renders an accessible %s notice without a severity prefix', (variant, role) => {
  const { container } = render(Alert, { variant, children });
  const alert = screen.getByRole(role);
  expect(alert).toHaveAttribute('data-variant', variant);
  expect(alert).toHaveAttribute('aria-atomic', 'true');
  expect(alert).toHaveTextContent(/^Warcraft Logs needs a breather\.$/);
  expect(container.querySelector('svg')).toHaveAttribute('aria-hidden', 'true');
});

it('defaults to info and supports compact notices', async () => {
  const { rerender } = render(Alert, { children });
  expect(screen.getByRole('status')).toHaveAttribute('data-variant', 'info');
  expect(screen.getByRole('status')).not.toHaveClass('compact');
  await rerender({ children, variant: 'warning', compact: true });
  expect(screen.getByRole('status')).toHaveClass('compact');
  expect(screen.queryByRole('alert')).not.toBeInTheDocument();
});
