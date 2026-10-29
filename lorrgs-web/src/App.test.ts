import { fireEvent, render, screen, waitFor, within } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';
import App from './App.svelte';
import * as API from './lib/api';
import { getEditionSlug } from './lib/raidCatalog';
import { getVersionIconUrl } from './lib/selectionIcons';

describe('App version mapping', () => {
  function mockWorldData() {
    localStorage.setItem('lorrgs-theme', 'light');
    vi.spyOn(API, 'waitForHealthReady').mockResolvedValue(true);
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
      vi.spyOn(API, name).mockResolvedValue([]);
    }
  }

  it('passes the URL edition to world-data loading and reloads after edition changes', async () => {
    mockWorldData();
    history.replaceState({}, '', '/?edition=Anniversary');
    render(App);

    await screen.findByRole('heading', { name: 'Edition' });
    expect(API.getBosses).toHaveBeenLastCalledWith('anniversary');
    expect(API.getClasses).toHaveBeenLastCalledWith('anniversary');
    await fireEvent.click(screen.getByRole('button', { name: 'Retail' }));
    await waitFor(() => expect(API.getBosses).toHaveBeenLastCalledWith('midnight'));
    expect(API.getClasses).toHaveBeenLastCalledWith('midnight');
    expect(API.getSeasons).toHaveBeenLastCalledWith('midnight');
  });

  it('does not replace the current edition data with an older in-flight response', async () => {
    mockWorldData();
    history.replaceState({}, '', '/?edition=Anniversary');
    let resolveOlder: (value: unknown[]) => void = () => {};
    vi.mocked(API.getBosses).mockImplementation((edition) =>
      edition === 'midnight'
        ? new Promise((resolve) => {
            resolveOlder = resolve;
          })
        : Promise.resolve([])
    );
    render(App);
    await screen.findByRole('heading', { name: 'Edition' });
    await fireEvent.click(screen.getByRole('button', { name: 'Retail' }));
    await waitFor(() => expect(API.getBosses).toHaveBeenLastCalledWith('midnight'));
    await fireEvent.click(screen.getByRole('button', { name: 'Era' }));
    await waitFor(() => expect(API.getBosses).toHaveBeenLastCalledWith('era'));
    resolveOlder([{ id: 1, name: 'Wrong edition boss', nameSlug: 'wrong', icon: '', raidId: 1 }]);
    await fireEvent.click(screen.getByText('Tools'));
    await fireEvent.click(screen.getByRole('button', { name: 'World Data' }));
    await waitFor(() => expect(screen.queryByText('Wrong edition boss')).not.toBeInTheDocument());
  });

  it('uses the retail catalog key and retail icon for the Retail option', () => {
    expect(getEditionSlug('Retail')).toBe('retail');
    expect(getVersionIconUrl('Retail')).toBe('/images/wow/editions/midnight.png');
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
            public: true,
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
    const landing = (await screen.findByRole('heading', { name: 'Compare raid cooldowns' })).closest(
      'section'
    )!;
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
    expect(screen.queryByRole('heading', { name: 'Compare raid cooldowns' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Boss' })).toHaveTextContent('Illidan');
  });

  it('requires an encounter on the launcher but keeps toolbar ranking links available', async () => {
    mockWorldData();
    render(App);
    const landing = (await screen.findByRole('heading', { name: 'Compare raid cooldowns' })).closest(
      'section'
    )!;
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
    vi.spyOn(API, 'waitForHealthReady').mockImplementation(() => new Promise<boolean>(() => {}));
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
