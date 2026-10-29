import { render, screen } from '@testing-library/svelte';
import { expect, it } from 'vitest';
import Spinner from './Spinner.svelte';

it('renders a decorative, current-color loading icon without duplicating status announcements', () => {
  const { container } = render(Spinner, { inline: true });
  const icon = container.querySelector('svg')!;
  expect(icon).toHaveClass('loading-spinner');
  expect(icon).toHaveAttribute('aria-hidden', 'true');
  expect(icon).toHaveAttribute('focusable', 'false');
  expect(icon).toHaveAttribute('stroke', 'currentColor');
  expect(icon.querySelector('path')).toBeInTheDocument();
});

it('provides a centered loading status without requiring progress text', () => {
  render(Spinner);
  expect(screen.getByRole('status', { name: 'Loading' })).toHaveClass('spinner');
  expect(screen.getByRole('status')).not.toHaveClass('inline');
  expect(screen.getByRole('status').querySelector('.loading-text')).toBeNull();
});

it('shows optional reactive progress text underneath the spinner', async () => {
  const { rerender } = render(Spinner, { loadingText: 'Loading reports: 1 of 3 ready' });
  expect(screen.getByRole('status')).toHaveTextContent('Loading reports: 1 of 3 ready');
  await rerender({ loadingText: 'Loading reports: 2 of 3 ready' });
  expect(screen.getByRole('status').querySelector('.loading-text')).toHaveTextContent(
    '2 of 3 ready'
  );
});
