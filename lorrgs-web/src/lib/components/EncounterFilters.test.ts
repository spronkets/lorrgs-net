import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';
import type { RaidCatalog } from '../models/RaidCatalog';
import EncounterFilters from './EncounterFilters.svelte';

const catalog: RaidCatalog = {
  editions: [],
  instances: {
    anniversary: [
      {
        zoneId: 1,
        slug: 'black-temple',
        name: 'Black Temple',
        edition: 'anniversary',
        phase: 3,
        public: true,
        bosses: [
          { slug: 'illidan', name: 'Illidan Stormrage', mapped: true },
          { slug: 'najentus', name: 'High Warlord Najentus', mapped: true }
        ]
      }
    ],
    retail: [
      {
        zoneId: 2,
        slug: 'nerub-ar-palace',
        name: "Nerub'ar Palace",
        edition: 'retail',
        phase: 1,
        public: true,
        bosses: [{ slug: 'ulgrax', name: 'Ulgrax the Devourer', mapped: true }]
      }
    ]
  }
};

describe('EncounterFilters', () => {
  it('changes edition and clears encounter selections, then presents the new raid and boss options', async () => {
    const oneditionchange = vi.fn();
    render(EncounterFilters, {
      worldData: {
        bosses: [{ nameSlug: 'illidan', icon: 'illidan' }]
      },
      raidCatalog: catalog,
      selectedVersion: 'Anniversary',
      selectedRaid: 'black-temple',
      selectedBoss: 'illidan',
      oneditionchange
    });

    expect(screen.getByRole('button', { name: 'Boss' })).toHaveTextContent('Illidan Stormrage');
    await fireEvent.click(screen.getByRole('button', { name: 'Retail' }));
    expect(oneditionchange).toHaveBeenCalledExactlyOnceWith('Retail');

    const raid = screen.getByRole('combobox', { name: 'Raid' });
    expect(raid).toHaveValue('');
    expect(screen.getByRole('button', { name: 'Boss' })).toBeDisabled();
    expect(screen.getByRole('option', { name: "Nerub'ar Palace" })).toBeInTheDocument();

    await fireEvent.change(raid, { target: { value: 'nerub-ar-palace' } });
    expect(screen.getByRole('button', { name: 'Boss' })).toHaveTextContent('Ulgrax the Devourer');
    expect(screen.getByRole('option', { name: "Nerub'ar Palace" })).toBeInTheDocument();
  });

  it('does not notify for the already selected edition and handles an edition without catalog raids', async () => {
    const oneditionchange = vi.fn();
    render(EncounterFilters, {
      raidCatalog: catalog,
      selectedVersion: 'Era',
      selectedRaid: '',
      selectedBoss: '',
      oneditionchange
    });
    await fireEvent.click(screen.getByRole('button', { name: 'Era' }));
    expect(oneditionchange).not.toHaveBeenCalled();
    expect(screen.getByRole('combobox', { name: 'Raid' })).toHaveValue('');
    expect(screen.getByRole('button', { name: 'Boss' })).toBeDisabled();

    await fireEvent.click(screen.getByRole('button', { name: 'Retail' }));
    expect(oneditionchange).toHaveBeenCalledExactlyOnceWith('Retail');
  });

  it('updates local edition selection when no edition callback is provided', async () => {
    render(EncounterFilters, {
      raidCatalog: catalog,
      selectedVersion: 'Anniversary',
      selectedRaid: '',
      selectedBoss: ''
    });
    await fireEvent.click(screen.getByRole('button', { name: 'Retail' }));
    expect(screen.getByRole('combobox', { name: 'Raid' })).toHaveValue('');
    expect(screen.getByRole('option', { name: "Nerub'ar Palace" })).toBeInTheDocument();
  });

  it('ignores a repeated selection callback from the edition control', async () => {
    vi.doMock('./EditionSelector.svelte', () => ({
      default: (
        anchor: Node,
        props: { selectedVersion: string; onselect: (version: string) => void }
      ) => {
        const button = document.createElement('button');
        button.textContent = props.selectedVersion;
        button.addEventListener('click', () => props.onselect(props.selectedVersion));
        anchor.parentNode?.insertBefore(button, anchor);
      }
    }));
    try {
      vi.resetModules();
      const [{ default: IsolatedEncounterFilters }, testingLibrary] = await Promise.all([
        import('./EncounterFilters.svelte'),
        import('@testing-library/svelte')
      ]);
      const oneditionchange = vi.fn();
      testingLibrary.render(IsolatedEncounterFilters, {
        raidCatalog: catalog,
        selectedVersion: 'Retail',
        selectedRaid: '',
        selectedBoss: '',
        oneditionchange
      });
      await testingLibrary.fireEvent.click(
        testingLibrary.screen.getByRole('button', { name: 'Retail' })
      );
      expect(oneditionchange).not.toHaveBeenCalled();
    } finally {
      vi.doUnmock('./EditionSelector.svelte');
    }
  });
});
