import { cleanup, fireEvent, render, screen, within } from '@testing-library/svelte';
import { afterEach, expect, it, vi } from 'vitest';
import { getSpecRankings } from '../lib/api';
import HomePage from './HomePage.svelte';

vi.mock('../lib/api', () => ({ getSpecRankings: vi.fn() }));

const worldData = {
  bosses: [
    { id: 50609, nameSlug: 'illidan-stormrage', raidId: 1060, icon: 'achievement_boss_illidan.jpg' }
  ],
  classes: [{ id: 2, name: 'Paladin', nameSlug: 'paladin', color: '#f58cba', maskId: 2 }],
  specs: [
    {
      id: 70,
      name: 'Retribution',
      nameSlug: 'retribution',
      fullName: 'Retribution Paladin',
      fullNameSlug: 'retribution-paladin',
      classId: 2,
      roleId: 1
    }
  ],
  roles: [{ id: 1, name: 'Damage', nameSlug: 'damage', code: 'dps', color: '#ffffff' }]
};
const raidCatalog = {
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
        bosses: [{ id: 50609, name: 'Illidan Stormrage', slug: 'illidan-stormrage', mapped: true }]
      }
    ]
  }
};

afterEach(() => {
  window.history.replaceState({}, '', '/');
  vi.resetAllMocks();
});

function renderPage() {
  return render(HomePage, { worldData, raidCatalog });
}

it('does not request rankings for an unavailable raid restored from the URL', async () => {
  window.history.replaceState(
    {},
    '',
    '/?edition=Anniversary&raid=blacktemple&boss=illidan-stormrage&spec=retribution-paladin'
  );
  render(HomePage, {
    worldData,
    raidCatalog: {
      ...raidCatalog,
      instances: {
        anniversary: raidCatalog.instances.anniversary.map((raid) => ({
          ...raid,
          isAvailable: false
        }))
      }
    }
  });
  expect(await screen.findByRole('option', { name: 'Black Temple' })).toBeDisabled();
  expect(screen.getByRole('combobox', { name: 'Raid' })).toHaveValue('');
  expect(getSpecRankings).not.toHaveBeenCalled();
});

it('combines the spec icons and encounter heading inside the selection panel', async () => {
  window.history.replaceState(
    {},
    '',
    '/?edition=Anniversary&raid=blacktemple&boss=illidan-stormrage&spec=retribution-paladin'
  );
  vi.mocked(getSpecRankings).mockResolvedValue({
    specSlug: 'retribution-paladin',
    bossSlug: 'illidan-stormrage',
    bossIconUrl: 'https://wow.zamimg.com/images/wow/icons/medium/inv_misc_questionmark.jpg',
    difficulty: 'normal',
    metric: 'dps',
    updated: '2026-10-26T12:00:00Z',
    isDirty: false,
    percentile: null,
    reports: []
  });
  const { container } = renderPage();
  const heading = await screen.findByRole('heading', {
    name: 'Paladin Retribution Paladin vs. Illidan Stormrage'
  });
  expect(screen.getByRole('heading', { name: 'Spec Rankings', level: 2 })).toBeInTheDocument();
  expect(container.querySelector('.page-header')).toHaveTextContent(
    'Compare cooldown usage across ranked players of a chosen specialization.'
  );
  expect(heading.closest('.selection-collapsed')).not.toBeNull();
  expect(container.querySelector('.selection-overview .value')).toHaveTextContent(
    /^Anniversary \/ Black Temple$/
  );
  expect(heading.querySelector('.spec-icon')?.nextElementSibling).toHaveClass('class-icon');
  expect(within(heading).getByRole('img', { name: 'Paladin' })).toBeInTheDocument();
  await screen.findByRole('region', { name: 'Cooldown comparison timelines' });
  expect(vi.mocked(getSpecRankings).mock.calls[0][5]).toMatchObject({
    includeOtherPlayers: false
  });
  const expectedIcon =
    'https://wow.zamimg.com/images/wow/icons/medium/achievement_boss_illidan.jpg';
  expect(heading.querySelector('.boss-icon')).toHaveAttribute('src', expectedIcon);
  expect(container.querySelector('.rankings-header')).toBeNull();
  expect(screen.queryByText(/Updated:/)).not.toBeInTheDocument();
  await fireEvent.click(screen.getByRole('button', { name: 'Change selection' }));
  const bossSelector = screen.getByRole('button', { name: 'Boss' });
  expect(bossSelector.querySelector('img')).toHaveAttribute('src', expectedIcon);
  await fireEvent.click(bossSelector);
  expect(
    screen.getByRole('option', { name: 'Illidan Stormrage' }).querySelector('img')
  ).toHaveAttribute('src', expectedIcon);
  expect(screen.queryByRole('heading', { name: /vs\. Illidan/ })).not.toBeInTheDocument();
  expect(screen.getByRole('button', { name: /Retribution Paladin/ })).toBeInTheDocument();
});

it('selects an encounter before loading, then exposes API errors and lets the user change selection', async () => {
  window.history.replaceState({}, '', '/?page=spec&version=legacy&size=20');
  const error = vi.spyOn(console, 'error').mockImplementation(() => {});
  vi.mocked(getSpecRankings).mockRejectedValueOnce('upstream unavailable');
  renderPage();

  const spec = screen.getByRole('button', { name: /Retribution Paladin/ });
  await fireEvent.click(spec);
  expect(getSpecRankings).not.toHaveBeenCalled();
  expect(spec).toHaveAttribute('title', 'Select a boss first');

  await fireEvent.change(screen.getByLabelText('Raid'), { target: { value: 'blacktemple' } });
  await fireEvent.click(screen.getByRole('button', { name: /Retribution Paladin/ }));
  expect(await screen.findByText('Failed to load rankings.')).toBeInTheDocument();
  expect(getSpecRankings).toHaveBeenCalledWith(
    'retribution-paladin',
    'illidan-stormrage',
    'Normal',
    'dps',
    'anniversary',
    expect.objectContaining({ zoneId: 1060, encounterId: 50609, includeOtherPlayers: false })
  );
  expect(error).toHaveBeenCalled();
  expect(new URLSearchParams(location.search).has('version')).toBe(false);
  expect(new URLSearchParams(location.search).has('size')).toBe(false);

  await fireEvent.click(screen.getByRole('button', { name: 'Change selection' }));
  expect(screen.getByRole('button', { name: /Retribution Paladin/ })).toBeInTheDocument();
});

it('reports missing encounter mappings and distinguishes an empty ranking response', async () => {
  const unmappedRaidCatalog = {
    editions: [],
    instances: {
      anniversary: [
        {
          zoneId: 0,
          slug: 'blacktemple',
          name: 'Black Temple',
          edition: 'anniversary',
          phase: 3,
          isAvailable: true,
          bosses: [
            { id: 50609, name: 'Illidan Stormrage', slug: 'illidan-stormrage', mapped: true }
          ]
        }
      ]
    }
  };
  window.history.replaceState(
    {},
    '',
    '/?edition=Anniversary&raid=blacktemple&boss=illidan-stormrage&spec=retribution-paladin'
  );
  render(HomePage, {
    worldData: { ...worldData, bosses: [{ ...worldData.bosses[0], raidId: 0 }] },
    raidCatalog: unmappedRaidCatalog
  });
  expect(
    await screen.findByText(
      'Selected raid boss is missing zone mapping. Please choose a mapped raid.'
    )
  ).toBeInTheDocument();
  expect(getSpecRankings).not.toHaveBeenCalled();

  cleanup();
  window.history.replaceState(
    {},
    '',
    '/?edition=Anniversary&raid=blacktemple&boss=illidan-stormrage&spec=retribution-paladin'
  );
  vi.mocked(getSpecRankings).mockResolvedValueOnce(null as never);
  renderPage();
  expect(await screen.findByText('No rankings found for this selection')).toBeInTheDocument();
});

it('ignores invalid query selections and removes them from the shareable URL', async () => {
  window.history.replaceState(
    {},
    '',
    '/?edition=NotAnEdition&raid=missing-raid&boss=missing-boss&spec=missing-spec'
  );
  renderPage();
  expect(screen.getByRole('heading', { name: 'Spec Rankings' })).toBeInTheDocument();
  expect(screen.getByRole('button', { name: /Retribution Paladin/ })).toBeInTheDocument();
  expect(getSpecRankings).not.toHaveBeenCalled();
  expect(new URLSearchParams(location.search).get('edition')).toBe('Anniversary');
  expect(new URLSearchParams(location.search).has('raid')).toBe(false);
  expect(new URLSearchParams(location.search).has('boss')).toBe(false);
  expect(new URLSearchParams(location.search).has('spec')).toBe(false);
});

it('keeps a valid ranking selection when class metadata is missing and uses role metric fallback', async () => {
  window.history.replaceState(
    {},
    '',
    '/?edition=Anniversary&raid=blacktemple&boss=illidan-stormrage&spec=retribution-paladin'
  );
  vi.mocked(getSpecRankings).mockResolvedValueOnce(null as never);
  const sparseWorldData = {
    bosses: worldData.bosses,
    classes: [],
    specs: worldData.specs,
    roles: [
      { id: 1, name: 'Damage', nameSlug: 'damage', code: 'dps', color: '#fff', Metric: ' HPS ' }
    ]
  };
  const { container } = render(HomePage, { worldData: sparseWorldData, raidCatalog });

  expect(await screen.findByText('No rankings found for this selection')).toBeInTheDocument();
  expect(screen.getByRole('img', { name: 'Class' })).toBeInTheDocument();
  expect(container.querySelector('.selection-spec-media .spec-icon')).toHaveAttribute(
    'src',
    '/images/wow/specs/other-retribution.jpg'
  );
  expect(getSpecRankings).toHaveBeenCalledWith(
    'retribution-paladin',
    'illidan-stormrage',
    'Normal',
    'hps',
    'anniversary',
    expect.objectContaining({ className: undefined, specName: 'Retribution' })
  );
});
