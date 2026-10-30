import { fireEvent, render, screen } from '@testing-library/svelte';
import { afterEach, expect, it, vi } from 'vitest';
import type { WorldDataSnapshot } from '../lib/models/WorldDataSnapshot';
import type { RaidCatalog } from '../lib/models/RaidCatalog';
import LandingPage from './LandingPage.svelte';

const worldData: WorldDataSnapshot = {
  bosses: [{ id: 50609, nameSlug: 'illidan', icon: 'illidan.jpg', raidId: 1060 }]
};
const raidCatalog: RaidCatalog = {
  editions: [],
  instances: {
    anniversary: [
      {
        zoneId: 1060,
        slug: 'blacktemple',
        name: 'Black Temple',
        edition: 'anniversary',
        phase: 3,
        isAvailable: true,
        bosses: [{ id: 50609, name: 'Illidan', slug: 'illidan', mapped: true }]
      }
    ],
    classic: [
      {
        zoneId: 100,
        slug: 'molten-core',
        name: 'Molten Core',
        edition: 'era',
        phase: 1,
        isAvailable: true,
        bosses: [{ id: 1001, name: 'Ragnaros', slug: 'ragnaros', mapped: true }]
      }
    ]
  }
};

afterEach(() => {
  window.history.replaceState({}, '', '/');
});

it('hydrates a shared encounter, navigates to either ranking flow, and retains unrelated URL state', async () => {
  window.history.replaceState(
    {},
    '',
    '/?page=landing&edition=Anniversary&raid=blacktemple&boss=illidan#about'
  );
  const oneditionchange = vi.fn();
  const onnavigate = vi.fn();

  render(LandingPage, {
    worldData,
    raidCatalog,
    selectedVersion: 'Anniversary',
    oneditionchange,
    onnavigate
  });

  expect(screen.queryByRole('heading', { name: 'Compare raid cooldowns' })).not.toBeInTheDocument();
  expect(screen.getByRole('heading', { name: 'Edition' })).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Spec Rankings' })).toBeEnabled();
  expect(screen.getByRole('button', { name: 'Comp Rankings' })).toBeEnabled();
  expect(location.search).toContain('edition=Anniversary');
  expect(location.hash).toBe('#about');

  await fireEvent.click(screen.getByRole('button', { name: 'Spec Rankings' }));
  await fireEvent.click(screen.getByRole('button', { name: 'Comp Rankings' }));
  expect(onnavigate.mock.calls).toEqual([['spec'], ['comp']]);

  await fireEvent.click(screen.getByRole('button', { name: 'Era' }));
  expect(oneditionchange).toHaveBeenCalledWith('Era');
  expect(screen.getByRole('button', { name: 'Spec Rankings' })).toBeDisabled();
  expect(screen.getByRole('button', { name: 'Comp Rankings' })).toBeDisabled();
  expect(location.search).toContain('edition=Era');
  expect(location.search).not.toContain('raid=');
  expect(location.search).not.toContain('boss=');

  await fireEvent.change(screen.getByLabelText('Raid'), { target: { value: 'molten-core' } });
  expect(screen.getByRole('button', { name: 'Spec Rankings' })).toBeEnabled();
  expect(location.search).toContain('raid=molten-core');
  expect(location.search).toContain('boss=ragnaros');
});

it('keeps ranking actions disabled until an encounter is selected and works without optional callbacks', async () => {
  window.history.replaceState({}, '', '/?page=landing&edition=Anniversary&keep=1');
  render(LandingPage, { worldData, raidCatalog, selectedVersion: 'Anniversary' });

  expect(screen.getByRole('button', { name: 'Spec Rankings' })).toBeDisabled();
  expect(screen.getByRole('button', { name: 'Comp Rankings' })).toBeDisabled();
  await fireEvent.change(screen.getByLabelText('Raid'), { target: { value: 'blacktemple' } });
  expect(screen.getByRole('button', { name: 'Spec Rankings' })).toBeEnabled();
  await fireEvent.click(screen.getByRole('button', { name: 'Spec Rankings' }));
  expect(location.search).toContain('keep=1');
  expect(location.search).toContain('boss=illidan');
});
