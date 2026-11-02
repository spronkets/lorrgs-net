import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { afterEach, expect, it, vi } from 'vitest';
import { getBossTimeline, getReportTimeline, getSpecRankings, lookupReport } from '../lib/api';
import { bossResponse, response, rows } from '../lib/testing/timelineFixtures';
import HomePage from './HomePage.svelte';

vi.mock('../lib/api', () => ({
  getSpecRankings: vi.fn(),
  lookupReport: vi.fn(),
  getBossTimeline: vi.fn(),
  getReportTimeline: vi.fn()
}));

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

const retailRaidCatalog = {
  editions: [],
  instances: {
    retail: [
      {
        zoneId: 1060,
        slug: 'blacktemple',
        name: 'Black Temple',
        edition: 'retail',
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

it('resolves a linked fight before requesting rankings and corrects the encounter in the URL', async () => {
  window.history.replaceState({}, '',
    '/?page=spec&edition=Anniversary&raid=hyjalsummit&boss=archimonde&spec=retribution-paladin&reportCode=LinkedReport&fightId=30'
  );
  vi.mocked(lookupReport).mockResolvedValue({
    reportData: { report: {
      code: 'LinkedReport', title: 'Linked Illidan', startTime: 0,
      fights: [{ id: 30, name: 'Illidan Stormrage', encounterID: 50609, kill: true,
        difficulty: 3, startTime: 1000, endTime: 301000 }]
    } }
  });
  vi.mocked(getSpecRankings).mockResolvedValue({
    specSlug: 'retribution-paladin', bossSlug: 'illidan-stormrage', difficulty: 'normal',
    metric: 'dps', updated: '', isDirty: false, percentile: null, reports: rows.map(row => row.report)
  });
  vi.mocked(getBossTimeline).mockImplementation(async report => bossResponse(report));
  vi.mocked(getReportTimeline).mockImplementation(async report => response(report));
  renderPage();
  await screen.findByText('vs.');
  await waitFor(() => {
    expect(getSpecRankings).toHaveBeenCalledWith(
      'retribution-paladin',
      'illidan-stormrage',
      'Normal',
      'dps',
      'anniversary',
      expect.any(Object)
    );
  });
  expect(new URLSearchParams(window.location.search).get('raid')).toBe('blacktemple');
  expect(new URLSearchParams(window.location.search).get('boss')).toBe('illidan-stormrage');
  await screen.findByText(/Boss reference: Linked Illidan, fight 30/);
  expect(getBossTimeline).toHaveBeenCalledWith(
    expect.objectContaining({ reportId: 'LinkedReport', fights: [expect.objectContaining({ fightId: 30 })] }),
    'anniversary', expect.any(AbortSignal)
  );
});

it('surfaces linked report errors instead of loading rankings for a mismatched encounter', async () => {
  window.history.replaceState({}, '',
    '/?edition=Anniversary&raid=blacktemple&boss=illidan-stormrage&spec=retribution-paladin&reportCode=LinkedReport&fightId=0'
  );
  const logged = vi.spyOn(console, 'error').mockImplementation(() => {});
  try {
    renderPage();
    expect(await screen.findByRole('alert')).toHaveTextContent('positive fight ID');
    expect(getSpecRankings).not.toHaveBeenCalled();
    expect(lookupReport).not.toHaveBeenCalled();
  } finally {
    logged.mockRestore();
  }
});

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

it('combines the spec icons and selection separator inside the selection panel', async () => {
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
  const separator = await screen.findByText('vs.');
  expect(screen.getByRole('heading', { name: 'Spec Rankings', level: 2 })).toBeInTheDocument();
  expect(container.querySelector('.page-header')).toHaveTextContent(
    'Compare cooldown usage across ranked players of a chosen specialization.'
  );
  expect(separator.closest('.selection-separator')).not.toBeNull();
  expect(container.querySelector('.spec-selector .spec-icon')?.nextElementSibling).toHaveClass(
    'class-icon'
  );
  await screen.findByRole('region', { name: 'Cooldown comparison timelines' });
  expect(vi.mocked(getSpecRankings).mock.calls[0][5]).toMatchObject({
    includeOtherPlayers: false
  });
  const expectedIcon =
    'https://wow.zamimg.com/images/wow/icons/medium/achievement_boss_illidan.jpg';
  expect(container.querySelector('.raid-boss-row .boss-selector .boss-icon')).toHaveAttribute(
    'src',
    expectedIcon
  );
  expect(container.querySelector('.rankings-header')).toBeNull();
  expect(screen.queryByText(/Updated:/)).not.toBeInTheDocument();
  expect(screen.getByText('Specialization')).toBeInTheDocument();
  expect(screen.queryByRole('combobox', { name: 'Role' })).not.toBeInTheDocument();
  expect(screen.queryByRole('combobox', { name: 'Class' })).not.toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Retribution Paladin' })).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Boss' })).toBeInTheDocument();
});

it('shows difficulty beside boss for retail and uses the selected difficulty', async () => {
  window.history.replaceState(
    {},
    '',
    '/?edition=Retail&raid=blacktemple&boss=illidan-stormrage&spec=retribution-paladin&difficultyId=4'
  );
  vi.mocked(getSpecRankings).mockResolvedValue({
    specSlug: 'retribution-paladin',
    bossSlug: 'illidan-stormrage',
    difficulty: 'heroic',
    metric: 'dps',
    updated: '',
    isDirty: false,
    percentile: null,
    reports: []
  });
  render(HomePage, { worldData, raidCatalog: retailRaidCatalog });
  await screen.findByText('vs.');
  const difficulty = screen.getByRole('combobox', { name: 'Difficulty' });
  expect(difficulty).toHaveValue('4');
  expect(getSpecRankings).toHaveBeenCalledWith(
    'retribution-paladin',
    'illidan-stormrage',
    'Heroic',
    'dps',
    'retail',
    expect.objectContaining({ difficultyId: 4 })
  );
  await fireEvent.change(difficulty, { target: { value: '5' } });
  await waitFor(() => {
    expect(getSpecRankings).toHaveBeenCalledWith(
      'retribution-paladin',
      'illidan-stormrage',
      'Mythic',
      'dps',
      'retail',
      expect.objectContaining({ difficultyId: 5 })
    );
  });
});

it('selects an encounter before loading, then exposes API errors and lets the user change selection', async () => {
  window.history.replaceState({}, '', '/?page=spec&version=legacy&size=20');
  const error = vi.spyOn(console, 'error').mockImplementation(() => {});
  vi.mocked(getSpecRankings).mockRejectedValueOnce('upstream unavailable');
  renderPage();

  const specSelect = screen.getByRole('button', { name: 'Retribution Paladin' });
  expect(getSpecRankings).not.toHaveBeenCalled();
  expect(specSelect).toBeEnabled();

  await fireEvent.change(screen.getByLabelText('Raid'), { target: { value: 'blacktemple' } });
  await fireEvent.click(screen.getByRole('button', { name: 'Retribution Paladin' }));
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
  expect(screen.getByText('Specialization')).toBeInTheDocument();
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
  expect(container.querySelector('.spec-selector .class-icon')).not.toBeNull();
  expect(container.querySelector('.spec-selector .spec-icon')).toHaveAttribute(
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
