import * as WorldDataApi from './lib/worldDataApi';
import * as HttpApi from './lib/common/http/apiClient';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';
import App from './App.svelte';
import * as API from './lib/api';
import { getEditionSlug } from './lib/raidCatalog';
import { getVersionIconUrl } from './lib/selectionIcons';

describe('App version mapping', () => {
  function mockWorldData() {
    localStorage.setItem('lorrgs-theme', 'light');
    vi.spyOn(HttpApi, 'waitForHealthReady').mockResolvedValue(true);
    vi.spyOn(API, 'getRaidCatalog').mockResolvedValue({ editions: [], instances: {} });
    for (const name of [
      'getClasses',
      'getSpecs',
      'getRoles',
      'getBosses',
      'getZones',
      'getSpells',
      'getTrinkets',
      'getSeasons'
    ] as const) {
      vi.spyOn(WorldDataApi, name).mockResolvedValue([]);
    }
  }

  it('passes the URL edition to world-data loading and reloads after edition changes', async () => {
    mockWorldData();
    history.replaceState({}, '', '/?edition=Anniversary');
    render(App);

    await screen.findByRole('heading', { name: 'Edition' });
    expect(WorldDataApi.getBosses).toHaveBeenLastCalledWith('anniversary');
    expect(WorldDataApi.getClasses).toHaveBeenLastCalledWith('anniversary');
    await fireEvent.click(screen.getByRole('button', { name: 'Retail' }));
    await waitFor(() => expect(WorldDataApi.getBosses).toHaveBeenLastCalledWith('midnight'));
    expect(WorldDataApi.getClasses).toHaveBeenLastCalledWith('midnight');
    expect(WorldDataApi.getSeasons).toHaveBeenLastCalledWith('midnight');
  });

  it('does not replace the current edition data with an older in-flight response', async () => {
    mockWorldData();
    history.replaceState({}, '', '/?edition=Anniversary');
    let resolveOlder: (
      value: Awaited<ReturnType<typeof WorldDataApi.getBosses>>
    ) => void = () => {};
    vi.mocked(WorldDataApi.getBosses).mockImplementation((edition) =>
      edition === 'midnight'
        ? new Promise((resolve) => {
            resolveOlder = resolve;
          })
        : Promise.resolve([])
    );
    render(App);
    await screen.findByRole('heading', { name: 'Edition' });
    await fireEvent.click(screen.getByRole('button', { name: 'Retail' }));
    await waitFor(() => expect(WorldDataApi.getBosses).toHaveBeenLastCalledWith('midnight'));
    await fireEvent.click(screen.getByRole('button', { name: 'Era' }));
    await waitFor(() => expect(WorldDataApi.getBosses).toHaveBeenLastCalledWith('era'));
    resolveOlder([{ id: 1, name: 'Wrong edition boss', nameSlug: 'wrong', icon: '', raidId: 1 }]);
    await fireEvent.click(screen.getByText('Tools'));
    await fireEvent.click(screen.getByRole('button', { name: 'World Data' }));
    await waitFor(() => expect(screen.queryByText('Wrong edition boss')).not.toBeInTheDocument());
  });

  it('uses the retail catalog key and retail icon for the Retail option', () => {
    expect(getEditionSlug('Retail')).toBe('retail');
    expect(getVersionIconUrl('Retail')).toBe('/images/wow/editions/midnight.png');
  });

  it('opens manual reports from navigation and supports direct links and edition changes', async () => {
    mockWorldData();
    history.replaceState({}, '', '/?page=reports&edition=Anniversary');
    render(App);
    await screen.findByRole('heading', { name: 'Load Report' });
    expect(screen.getByRole('button', { name: 'Load Report' })).toHaveAttribute('aria-current', 'page');
    history.replaceState({}, '', '/?page=reports&edition=Anniversary&report=ReportA&reportBoss=ReportA:10&pull=ReportA:1&loadedPull=ReportA:1');
    await fireEvent.click(screen.getByRole('button', { name: 'Era' }));
    await waitFor(() => expect(WorldDataApi.getClasses).toHaveBeenLastCalledWith('era'));
    await screen.findByRole('heading', { name: 'Load Report' });
    expect(new URLSearchParams(location.search).get('edition')).toBe('Era');
    for (const key of ['report', 'reportBoss', 'pull', 'loadedPull'])
      expect(new URLSearchParams(location.search).has(key)).toBe(false);
    await fireEvent.click(screen.getByRole('button', { name: 'LorrgsNET home' }));
    await screen.findByRole('heading', { name: 'Compare raid cooldowns' });
    await fireEvent.click(screen.getByRole('button', { name: 'Load Report' }));
    await screen.findByRole('heading', { name: 'Load Report' });
    expect(new URLSearchParams(location.search).get('page')).toBe('reports');
  });

  it.each([true, false])(
    'uses the system theme when no saved preference exists (dark=%s)',
    async (dark) => {
      mockWorldData();
      localStorage.removeItem('lorrgs-theme');
      vi.stubGlobal(
        'matchMedia',
        vi.fn(() => ({ matches: dark }))
      );
      render(App);
      await screen.findByRole('heading', { name: 'Compare raid cooldowns' });
      await fireEvent.click(
        screen.getByRole('button', { name: dark ? 'Switch to light mode' : 'Switch to dark mode' })
      );
      expect(localStorage.getItem('lorrgs-theme')).toBe(dark ? 'light' : 'dark');
    }
  );

  it.each([new Error('Catalog unavailable'), 'Invalid response'])(
    'surfaces startup failures: %s',
    async (failure) => {
      mockWorldData();
      vi.spyOn(console, 'error').mockImplementation(() => {});
      vi.mocked(API.getRaidCatalog).mockRejectedValue(failure);
      render(App);
      expect(await screen.findByRole('alert')).toHaveTextContent(
        failure instanceof Error ? failure.message : 'Unknown error'
      );
      expect(console.error).toHaveBeenCalledWith('Failed to load world data:', failure);
    }
  );

  it('does not request catalogs when the API fails its readiness check', async () => {
    mockWorldData();
    vi.spyOn(console, 'error').mockImplementation(() => {});
    vi.mocked(HttpApi.waitForHealthReady).mockResolvedValue(false);
    render(App);
    expect(await screen.findByRole('alert')).toHaveTextContent('API is not ready yet');
    expect(API.getRaidCatalog).not.toHaveBeenCalled();
  });

  it('opens the analyzer through Tools and honors direct world-data navigation', async () => {
    mockWorldData();
    history.replaceState({}, '', '/?page=worlddata&edition=Anniversary');
    render(App);
    await screen.findByRole('heading', { name: /World Data/i });
    await fireEvent.click(screen.getByText('Tools'));
    await fireEvent.click(screen.getByRole('button', { name: 'Rotation Analyzer' }));
    expect(await screen.findByRole('heading', { name: 'Rotation Analyzer' })).toBeInTheDocument();
    expect(new URLSearchParams(location.search).get('page')).toBe('rotation');
  });

  it('launches separate ranking flows from home with the shared encounter selection', async () => {
    mockWorldData();
    vi.mocked(API.getRaidCatalog).mockResolvedValue({
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
        ]
      }
    });
    vi.spyOn(API, 'getCompRankings').mockResolvedValue({
      bossSlug: 'illidan',
      difficulty: '3',
      updated: '',
      page: 1,
      totalCount: 0,
      hasMorePages: false,
      reports: []
    });
    history.replaceState({}, '', '/?edition=Anniversary&raid=blacktemple&boss=illidan');
    render(App);
    const landing = (
      await screen.findByRole('heading', { name: 'Compare raid cooldowns' })
    ).closest('section')!;
    expect(API.getCompRankings).not.toHaveBeenCalled();
    await fireEvent.click(within(landing).getByRole('button', { name: 'Comp Rankings' }));
    await screen.findByRole('button', { name: 'Search compositions' });
    expect(screen.getAllByRole('button', { name: 'Boss' })).toHaveLength(1);
    expect(screen.getByRole('button', { name: 'Boss' })).toHaveTextContent('Illidan');
    expect(screen.getByRole('combobox', { name: 'Raid' })).toHaveValue('blacktemple');
    expect(API.getCompRankings).not.toHaveBeenCalled();
    await fireEvent.click(screen.getByRole('button', { name: 'Search compositions' }));
    await waitFor(() =>
      expect(API.getCompRankings).toHaveBeenCalledWith(
        'illidan',
        [],
        [],
        'anniversary',
        expect.objectContaining({ encounterId: 50609 }),
        expect.any(AbortSignal)
      )
    );
    const nav = screen.getByRole('navigation', { name: 'Main navigation' });
    await fireEvent.click(within(nav).getByRole('button', { name: 'Spec Rankings' }));
    expect(screen.queryByRole('button', { name: 'Search compositions' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Boss' })).toHaveTextContent('Illidan');
    expect(new URLSearchParams(location.search).get('page')).toBe('spec');
    await fireEvent.click(screen.getByRole('button', { name: 'LorrgsNET home' }));
    await screen.findByRole('heading', { name: 'Compare raid cooldowns' });
    expect(screen.getByRole('button', { name: 'Boss' })).toHaveTextContent('Illidan');
    await fireEvent.click(
      within(
        screen.getByRole('heading', { name: 'Compare raid cooldowns' }).closest('section')!
      ).getByRole('button', { name: 'Spec Rankings' })
    );
    expect(new URLSearchParams(location.search).get('page')).toBe('spec');
    expect(
      screen.queryByRole('heading', { name: 'Compare raid cooldowns' })
    ).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Boss' })).toHaveTextContent('Illidan');
  });

  it('resolves an unmapped Archimonde selection when switching from spec to composition rankings', async () => {
    mockWorldData();
    vi.mocked(API.getRaidCatalog).mockResolvedValue({
      editions: [],
      instances: {
        anniversary: [
          {
            zoneId: 1060,
            slug: 'hyjalsummit',
            name: 'Hyjal Summit',
            edition: 'anniversary',
            phase: 3,
            isAvailable: true,
            bosses: [{ id: 0, name: 'Archimonde', slug: '', mapped: false }]
          }
        ]
      }
    });
    vi.mocked(WorldDataApi.getClasses).mockResolvedValue([
      { id: 2, name: 'Paladin', nameSlug: 'paladin', color: '#f58cba', maskId: 2 }
    ]);
    vi.mocked(WorldDataApi.getRoles).mockResolvedValue([
      { id: 1, name: 'Damage', nameSlug: 'damage', code: 'dps', color: '#ffffff' }
    ]);
    vi.mocked(WorldDataApi.getSpecs).mockResolvedValue([
      {
        id: 70,
        name: 'Retribution',
        nameSlug: 'retribution',
        fullName: 'Retribution Paladin',
        fullNameSlug: 'retribution-paladin',
        classId: 2,
        roleId: 1
      }
    ]);
    vi.spyOn(API, 'getSpecRankings').mockResolvedValue({
      specSlug: 'retribution-paladin',
      bossSlug: 'archimonde',
      difficulty: 'normal',
      metric: 'dps',
      updated: '',
      isDirty: false,
      percentile: null,
      reports: []
    });
    vi.spyOn(API, 'getCompRankings').mockResolvedValue({
      bossSlug: 'archimonde',
      difficulty: '3',
      updated: '',
      page: 1,
      totalCount: 0,
      hasMorePages: false,
      reports: []
    });
    history.replaceState(
      {},
      '',
      '/?page=spec&edition=Anniversary&raid=hyjalsummit&boss=archimonde&spec=retribution-paladin'
    );
    render(App);
    await fireEvent.click(await screen.findByRole('button', { name: 'Change selection' }));
    expect(screen.getByRole('combobox', { name: 'Raid' })).toHaveValue('hyjalsummit');
    expect(screen.getByRole('button', { name: 'Boss' })).toHaveTextContent('Archimonde');
    const nav = screen.getByRole('navigation', { name: 'Main navigation' });
    await fireEvent.click(within(nav).getByRole('button', { name: 'Comp Rankings' }));
    expect(screen.getByRole('combobox', { name: 'Raid' })).toHaveValue('hyjalsummit');
    expect(screen.getByRole('button', { name: 'Boss' })).toHaveTextContent('Archimonde');
    await fireEvent.click(screen.getByRole('button', { name: 'Search compositions' }));
    await waitFor(() =>
      expect(API.getCompRankings).toHaveBeenCalledWith(
        'archimonde',
        [],
        [],
        'anniversary',
        expect.objectContaining({ encounterId: 0, zoneId: 1060 }),
        expect.any(AbortSignal)
      )
    );
    expect(
      screen.queryByText('Select an edition, raid, and boss before searching.')
    ).not.toBeInTheDocument();
  });

  it('requires an encounter on the launcher but keeps toolbar ranking links available', async () => {
    mockWorldData();
    render(App);
    const landing = (
      await screen.findByRole('heading', { name: 'Compare raid cooldowns' })
    ).closest('section')!;
    expect(within(landing).getByRole('heading', { name: 'What is this?' })).toBeInTheDocument();
    expect(
      within(landing).getByRole('heading', { name: 'Disclaimer: use timings as a reference' })
    ).toBeInTheDocument();
    expect(within(landing).getByRole('button', { name: 'Spec Rankings' })).toBeDisabled();
    expect(within(landing).getByRole('button', { name: 'Comp Rankings' })).toBeDisabled();
    const nav = screen.getByRole('navigation', { name: 'Main navigation' });
    await fireEvent.click(within(nav).getByRole('button', { name: 'Comp Rankings' }));
    await screen.findByRole('button', { name: 'Search compositions' });
    expect(screen.getByRole('region', { name: 'Encounter selection' })).toBeInTheDocument();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
    localStorage.clear();
    history.replaceState({}, '', '/');
  });

  it('keeps navigation compact, closes Tools on selection and separates theme from the brand', async () => {
    vi.spyOn(HttpApi, 'waitForHealthReady').mockImplementation(
      () => new Promise<boolean>(() => {})
    );
    localStorage.setItem('lorrgs-theme', 'light');
    render(App);
    const nav = screen.getByRole('navigation', { name: 'Main navigation' });
    expect(nav).not.toHaveTextContent('Warcraft Logs rankings');
    expect(screen.getByText('lorrgs.io').closest('footer')).not.toBeNull();
    const footer = screen.getByRole('contentinfo');
    expect(footer).toHaveTextContent(
      'Combat data is retrieved from Warcraft Logs, and item and ability tooltips are provided by Wowhead.'
    );
    expect(footer).toHaveTextContent('copyright Blizzard Entertainment, Inc.');
    expect(footer).toHaveTextContent(
      'trademarks or registered trademarks of Blizzard Entertainment, Inc. in the U.S. and/or other countries.'
    );
    expect(footer).toHaveTextContent(
      'not affiliated with or endorsed by lorrgs.io, Warcraft Logs, Wowhead, or Blizzard Entertainment.'
    );
    expect(screen.getByRole('link', { name: 'Warcraft Logs' })).toHaveAttribute(
      'href',
      'https://www.warcraftlogs.com/'
    );
    expect(screen.getByRole('link', { name: 'Wowhead' })).toHaveAttribute(
      'href',
      'https://www.wowhead.com/'
    );
    const tools = screen.getByText('Tools').closest('details')!;
    expect(tools).not.toHaveAttribute('open');
    await fireEvent.click(screen.getByText('Tools'));
    expect(tools).toHaveAttribute('open');
    await fireEvent.click(screen.getByRole('button', { name: 'World Data' }));
    expect(tools).not.toHaveAttribute('open');
    await waitFor(() => expect(new URLSearchParams(location.search).get('page')).toBe('worlddata'));
    await fireEvent.click(screen.getByRole('button', { name: 'Switch to dark mode' }));
    expect(localStorage.getItem('lorrgs-theme')).toBe('dark');
    expect(new URLSearchParams(location.search).get('page')).toBe('worlddata');
    await fireEvent.click(screen.getByRole('button', { name: 'LorrgsNET home' }));
    await waitFor(() => expect(new URLSearchParams(location.search).has('page')).toBe(false));
    expect(localStorage.getItem('lorrgs-theme')).toBe('dark');
    await fireEvent.click(screen.getByText('Tools'));
    await fireEvent.keyDown(window, { key: 'Escape' });
    expect(tools).not.toHaveAttribute('open');
  });
});
