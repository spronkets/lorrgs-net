import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import * as API from '../lib/api';
import type { CompRankingData } from '../lib/models/CompRankingData';
import type { WorldDataSnapshot } from '../lib/models/WorldDataSnapshot';
import type { RaidCatalog } from '../lib/models/RaidCatalog';
import CompRankingsPage from './CompRankingsPage.svelte';

vi.mock('../lib/api', () => ({
  getCompRankings: vi.fn(),
  getCompRankingDetails: vi.fn(),
  getReportTimeline: vi.fn(),
  getBossTimeline: vi.fn()
}));

const worldData: WorldDataSnapshot = {
  roles: [
    { id: 2, name: 'Healer', nameSlug: 'healer', code: 'heal', color: '#fff' },
    { id: 3, name: 'Melee DPS', nameSlug: 'melee-dps', code: 'mdps', color: '#fff' }
  ],
  classes: [{ id: 2, name: 'Paladin', nameSlug: 'paladin', color: '#f58cba', maskId: 2 }],
  specs: [
    {
      id: 65,
      name: 'Holy',
      nameSlug: 'holy',
      fullName: 'Holy Paladin',
      fullNameSlug: 'holy-paladin',
      roleId: 2,
      classId: 2
    }
  ]
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
    ]
  }
};
const result: CompRankingData = {
  bossSlug: 'illidan',
  difficulty: '3',
  updated: '2026-10-28T00:00:00Z',
  page: 1,
  totalCount: 50,
  hasMorePages: true,
  reports: [
    {
      reportId: 'ReportA',
      title: 'Guild kill',
      percentile: null,
      duration: 200000,
      startTime: '2026-10-28T00:00:00Z',
      fights: [
        {
          fightId: 1,
          name: 'Illidan',
          duration: 200000,
          isKill: true,
          startTime: '2026-10-28T00:00:00Z',
          percentile: null,
          deaths: 0,
          damageTaken: 10000,
          composition: {
            specs: { 'holy-paladin': 1 },
            roles: { heal: 1, mdps: 0 },
            classes: { paladin: 1 }
          },
          phases: [{ id: 2, timestamp: 30000 }]
        }
      ],
      players: [
        {
          playerId: 7,
          name: 'Healer',
          classId: 2,
          specId: 65,
          specSlug: 'holy-paladin',
          performance: 0
        }
      ]
    }
  ]
};

function renderPage(query = '') {
  const params = new URLSearchParams(query);
  if (!params.has('compPage')) params.set('compPage', '1');
  window.history.replaceState(
    {},
    '',
    `/?page=comp&edition=Anniversary&raid=blacktemple&boss=illidan&${params}`
  );
  return render(CompRankingsPage, { worldData, raidCatalog, selectedVersion: 'Anniversary' });
}

beforeEach(() => {
  vi.mocked(API.getCompRankings).mockResolvedValue(result);
  vi.mocked(API.getCompRankingDetails).mockResolvedValue(result.reports[0]);
  vi.mocked(API.getReportTimeline).mockResolvedValue({
    reportCode: 'ReportA',
    fightId: 1,
    sourceId: 7,
    playerName: 'Healer',
    duration: 200000,
    isComplete: true,
    spells: [{ id: 31821, name: 'Aura Mastery', icon: null }],
    events: [{ spellId: 31821, timestamp: 10000, duration: null, kind: 'cast' }]
  });
  vi.mocked(API.getBossTimeline).mockResolvedValue({
    reportCode: 'ReportA',
    fightId: 1,
    name: 'Illidan',
    duration: 200000,
    isComplete: true,
    spells: [],
    events: [],
    phases: [{ id: 2, timestamp: 30000 }]
  });
});
afterEach(() => {
  window.history.replaceState({}, '', '/');
  vi.resetAllMocks();
});

it('does not request compositions for an unavailable raid restored from the URL', async () => {
  window.history.replaceState(
    {},
    '',
    '/?page=comp&edition=Anniversary&raid=blacktemple&boss=illidan&compPage=1'
  );
  render(CompRankingsPage, {
    worldData,
    selectedVersion: 'Anniversary',
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
  expect(API.getCompRankings).not.toHaveBeenCalled();
});

it('explains missing mappings instead of claiming a selected encounter is unselected', async () => {
  window.history.replaceState(
    {},
    '',
    '/?page=comp&edition=Anniversary&raid=hyjalsummit&boss=archimonde'
  );
  render(CompRankingsPage, {
    worldData,
    selectedVersion: 'Anniversary',
    raidCatalog: {
      editions: [],
      instances: {
        anniversary: [
          {
            ...raidCatalog.instances.anniversary[0],
            zoneId: 0,
            slug: 'hyjalsummit',
            name: 'Hyjal Summit',
            bosses: [{ id: 0, name: 'Archimonde', slug: '', mapped: false }]
          }
        ]
      }
    }
  });
  await fireEvent.click(screen.getByRole('button', { name: 'Search compositions' }));
  expect(screen.getByRole('alert')).toHaveTextContent(
    'Selected raid boss is missing encounter and zone mappings.'
  );
  expect(API.getCompRankings).not.toHaveBeenCalled();
});

it('hydrates repeated filters and kill times, then requests the correct edition and encounter', async () => {
  const { container } = renderPage(
    '&role=heal.gte.2&role=mdps.eq.0&spec=holy-paladin.lte.2&killtime_min=2:00&killtime_max=300&compPage=2'
  );
  await screen.findByText('No inspected kills match these filters on this page.');
  expect(API.getCompRankings).toHaveBeenCalledWith(
    'illidan',
    ['heal.gte.2', 'mdps.eq.0'],
    ['holy-paladin.lte.2'],
    'anniversary',
    expect.objectContaining({
      encounterId: 50609,
      difficultyId: 3,
      page: 2,
      minSeconds: 120,
      maxSeconds: 300
    }),
    expect.any(AbortSignal)
  );
  expect(screen.getByLabelText('Healer count')).toHaveValue(2);
  expect(screen.getByLabelText('Melee DPS count')).toHaveValue(0);
  expect(screen.getByLabelText('Holy Paladin count')).toHaveValue(2);
  expect(screen.getByLabelText('Minimum kill time')).toHaveValue('2:00');
  expect(screen.getByLabelText('Maximum kill time')).toHaveValue('5:00');
  expect(container.querySelector('.icon-select img')).toBeNull();
});

it('serializes zero-count comparisons without pagination or upstream refresh controls', async () => {
  renderPage();
  await screen.findByText('Roster & cooldown timelines (1 players)');
  await fireEvent.input(screen.getByLabelText('Healer count'), { target: { value: '0' } });
  await fireEvent.change(screen.getByLabelText('Healer comparison'), { target: { value: 'eq' } });
  await fireEvent.click(screen.getByRole('button', { name: 'Search compositions' }));
  await waitFor(() => expect(API.getCompRankings).toHaveBeenCalledTimes(2));
  expect(new URLSearchParams(location.search).getAll('role')).toEqual(['heal.eq.0']);
  await screen.findByText('No inspected kills match these filters on this page.');
  expect(screen.queryByRole('button', { name: 'Next page' })).not.toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Previous page' })).not.toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Refresh from Warcraft Logs' })).not.toBeInTheDocument();
});

it('edits direct spec counts and clears restrictions without treating blank as zero', async () => {
  renderPage();
  await screen.findByText('Roster & cooldown timelines (1 players)');
  expect(screen.getByLabelText('Holy Paladin comparison')).toBeDisabled();
  await fireEvent.input(screen.getByLabelText('Holy Paladin count'), { target: { value: '2' } });
  expect(screen.getByLabelText('Holy Paladin comparison')).not.toBeDisabled();
  await fireEvent.change(screen.getByLabelText('Holy Paladin comparison'), {
    target: { value: 'gte' }
  });
  await fireEvent.click(screen.getByRole('button', { name: 'Search compositions' }));
  await waitFor(() => expect(API.getCompRankings).toHaveBeenCalledTimes(2));
  expect(new URLSearchParams(location.search).getAll('spec')).toEqual(['holy-paladin.gte.2']);
  await fireEvent.input(screen.getByLabelText('Holy Paladin count'), { target: { value: '' } });
  await fireEvent.click(screen.getByRole('button', { name: 'Search compositions' }));
  await waitFor(() => expect(API.getCompRankings).toHaveBeenCalledTimes(3));
  expect(new URLSearchParams(location.search).getAll('spec')).toEqual([]);
});

it('rejects reversed kill-time ranges before requesting upstream', async () => {
  renderPage();
  await screen.findByText('Roster & cooldown timelines (1 players)');
  await fireEvent.input(screen.getByLabelText('Minimum kill time'), { target: { value: '05:00' } });
  await fireEvent.input(screen.getByLabelText('Maximum kill time'), { target: { value: '04:00' } });
  await fireEvent.click(screen.getByRole('button', { name: 'Search compositions' }));
  expect(await screen.findByText(/Enter kill times as m:ss/)).toBeInTheDocument();
  expect(API.getCompRankings).toHaveBeenCalledTimes(1);
});

it('masks typed kill times and sends their duration in seconds', async () => {
  renderPage();
  await screen.findByText('Roster & cooldown timelines (1 players)');
  await fireEvent.input(screen.getByLabelText('Minimum kill time'), { target: { value: '532' } });
  expect(screen.getByLabelText('Minimum kill time')).toHaveValue('5:32');
  await fireEvent.click(screen.getByRole('button', { name: 'Search compositions' }));
  await waitFor(() => expect(API.getCompRankings).toHaveBeenCalledTimes(2));
  expect(vi.mocked(API.getCompRankings).mock.calls.at(-1)?.[4].minSeconds).toBe(332);
});

it('notifies the parent of restored and newly selected editions through its callback', async () => {
  window.history.replaceState({}, '', '/?page=comp&edition=Anniversary');
  const oneditionchange = vi.fn();
  render(CompRankingsPage, { worldData, raidCatalog, oneditionchange });
  await waitFor(() => expect(oneditionchange).toHaveBeenCalledWith('Anniversary'));
  await fireEvent.click(screen.getByRole('button', { name: 'Retail' }));
  expect(oneditionchange).toHaveBeenLastCalledWith('Retail');
});

it('cancels stale searches when changing editions and ignores late responses', async () => {
  let resolve!: (_value: CompRankingData) => void;
  vi.mocked(API.getCompRankings).mockImplementationOnce(
    () =>
      new Promise((done) => {
        resolve = done;
      })
  );
  renderPage();
  await waitFor(() => expect(API.getCompRankings).toHaveBeenCalledTimes(1));
  const signal = vi.mocked(API.getCompRankings).mock.calls[0][5];
  await fireEvent.click(screen.getByRole('button', { name: 'Retail' }));
  expect(signal?.aborted).toBe(true);
  resolve(result);
  await waitFor(() => expect(screen.queryByText(/Guild kill/)).not.toBeInTheDocument());
});

it('restores searches on browser navigation and distinguishes errors from no matches', async () => {
  renderPage();
  await screen.findByText('Roster & cooldown timelines (1 players)');
  vi.mocked(API.getCompRankings).mockResolvedValueOnce({ ...result, reports: [], page: 2 });
  window.history.replaceState(
    {},
    '',
    '/?page=comp&edition=Anniversary&raid=blacktemple&boss=illidan&compPage=2'
  );
  window.dispatchEvent(new PopStateEvent('popstate'));
  expect(await screen.findByText(/No kill summaries on this page/)).toBeInTheDocument();
  vi.mocked(API.getCompRankings).mockRejectedValueOnce(
    new Error('Warcraft Logs rate limit reached')
  );
  await fireEvent.click(screen.getByRole('button', { name: 'Search compositions' }));
  expect(await screen.findByText('Warcraft Logs rate limit reached')).toBeInTheDocument();
  expect(screen.queryByText(/No kill summaries on this page/)).not.toBeInTheDocument();
});

it('opens real shared player and boss timelines with the Everything preset', async () => {
  const { container } = renderPage();
  await screen.findByText('Roster & cooldown timelines (1 players)');
  expect(API.getReportTimeline).not.toHaveBeenCalled();
  expect(container.querySelector('article h3 a')).toHaveAttribute(
    'href',
    'https://fresh.warcraftlogs.com/reports/ReportA#fight=1'
  );
  const details = screen.getByText('Roster & cooldown timelines (1 players)').closest('details')!;
  details.open = true;
  await fireEvent(details, new Event('toggle'));
  await screen.findByRole('region', { name: 'Cooldown comparison timelines' });
  expect(screen.getByRole('button', { name: 'Everything' })).toHaveAttribute(
    'aria-pressed',
    'true'
  );
  await waitFor(() => expect(container.querySelector('.player-row')).not.toBeNull());
  expect(container.querySelector('.boss-row')).not.toBeNull();
  expect(container.querySelector('.player-row')).toHaveTextContent('Healer');
  expect(API.getBossTimeline).toHaveBeenCalledWith(
    expect.objectContaining({ reportId: 'ReportA' }),
    'anniversary',
    expect.any(AbortSignal)
  );
  await fireEvent.click(screen.getByRole('button', { name: 'Melee DPS: 0' }));
  await waitFor(() => expect(container.querySelector('.player-row')).toBeNull());
  await fireEvent.click(screen.getByRole('button', { name: 'All: 1' }));
  await waitFor(() => expect(container.querySelector('.player-row')).toHaveTextContent('Healer'));
  expect(container).not.toHaveTextContent('bossIconName=');
});
