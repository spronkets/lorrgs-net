import { render, screen } from '@testing-library/svelte';
import { expect, it } from 'vitest';
import RankingPageHeader from './RankingPageHeader.svelte';

it('updates the page title and supporting description when the ranking context changes', async () => {
  const { rerender } = render(RankingPageHeader, {
    title: 'Spec Rankings',
    description: 'Compare players.'
  });
  expect(screen.getByRole('heading', { level: 2, name: 'Spec Rankings' })).toBeInTheDocument();
  expect(screen.getByText('Compare players.')).toBeInTheDocument();
  await rerender({ title: 'Comp Rankings', description: 'Compare raid compositions.' });
  expect(screen.queryByText('Spec Rankings')).not.toBeInTheDocument();
  expect(screen.getByRole('heading', { level: 2, name: 'Comp Rankings' })).toBeInTheDocument();
  expect(screen.getByText('Compare raid compositions.')).toBeInTheDocument();
});
