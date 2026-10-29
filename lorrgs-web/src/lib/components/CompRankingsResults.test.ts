import { fireEvent, render, screen, waitFor, within } from '@testing-library/svelte';
import { beforeEach, afterEach, expect, it, vi } from 'vitest';
import type { CompRankingData, RankingReport } from '../api';
import { getCompRankingDetails, getReportTimeline } from '../api';
import CompRankingsResults from './CompRankingsResults.svelte';

vi.mock('../api', () => ({
  getCompRankingDetails: vi.fn(),
  getReportTimeline: vi.fn(),
  getBossTimeline: vi.fn()
}));
const summaries: RankingReport[] = Array.from({ length: 50 }, (_, index) => ({
  reportId: `Report${index + 1}`,
  title: `Kill ${index + 1}`,
  duration: 200000,
  percentile: null,
  startTime: '2026-10-28T00:00:00Z',
  players: [],
  fights: [
    {
      fightId: index + 1,
      name: 'Boss',
      duration: 200000,
      isKill: true,
      startTime: '2026-10-28T00:00:00Z',
      percentile: null,
      deaths: 0,
      damageTaken: index + 1
    }
  ]
}));
const rankings: CompRankingData = {
  bossSlug: 'boss',
  difficulty: '3',
  page: 1,
  totalCount: 50,
  hasMorePages: true,
  updated: '2026-10-28T00:00:00Z',
  reports: summaries
};
function detail(summary: RankingReport, healers = 1): RankingReport {
  return {
    ...summary,
    title: summary.title,
    players: [
      {
        playerId: 7,
        name: 'Player',
        classId: 2,
        specId: 65,
        specSlug: 'holy-paladin',
        performance: 0
      }
    ],
    fights: [
      {
        ...summary.fights![0],
        composition: {
          roles: { heal: healers },
          specs: { 'holy-paladin': healers },
          classes: { paladin: healers }
        }
      }
    ]
  };
}
beforeEach(() => {
  vi.mocked(getCompRankingDetails).mockImplementation(async (summary) => detail(summary));
});
afterEach(() => vi.resetAllMocks());

it('sorts by ascending execution rank, keeping missing ranks last and ties stable', async () => {
  vi.mocked(getCompRankingDetails).mockImplementation(async (summary) => {
    const report = detail(summary);
    const rank = summary.reportId === 'Report1' ? 8 : 2;
    report.fights![0].execution = { rank, best: rank, totalParses: 100, rankPercent: 99 };
    report.fights![0].speed = { rank: 5, best: 4, totalParses: 100, rankPercent: 95 };
    report.guildName = 'Raid Guild';
    report.serverName = 'Thunderstrike';
    report.serverRegion = 'eu';
    return report;
  });
  const { container } = render(CompRankingsResults, {
    rankings,
    worldData: {},
    edition: 'anniversary'
  });
  await waitFor(() => {
    const headings = [...container.querySelectorAll('article > .heading h3')];
    expect(headings.slice(0, 4).map((heading) => heading.textContent)).toEqual([
      '#2 · Kill 2',
      '#2 · Kill 3',
      '#8 · Kill 1',
      'Rank unavailable · Kill 4'
    ]);
  });
  const first = container.querySelector('article')!;
  expect(within(first).getByText('Raid Guild · Thunderstrike EU')).toBeInTheDocument();
  expect(within(first).getByText('Speed').nextElementSibling).toHaveTextContent('#5 (Top 5%)');
  expect(within(first).getByText('Execution').nextElementSibling).toHaveTextContent('#2 (Top 1%)');
  expect(within(first).getByText('Speed').parentElement).toHaveAttribute(
    'title', 'Speed ranking out of 100 ranked kills'
  );
});

it('renders every loaded roster timeline without fetching players separately', async () => {
  vi.mocked(getCompRankingDetails).mockImplementation(async (summary) => {
    const report = detail(summary);
    report.bossTimeline = {
      reportCode: report.reportId,
      fightId: report.fights![0].fightId,
      name: 'Boss',
      duration: report.duration,
      isComplete: true,
      spells: [],
      events: [],
      phases: []
    };
    report.players = Array.from({ length: 5 }, (_, index) => ({
      ...report.players![0],
      playerId: index + 7,
      roleCode: index === 0 ? 'tank' : index === 1 ? 'heal' : 'rdps',
      parsePercentile: index === 0 ? 93 : 88,
      name: `Player ${index + 1}`,
      timeline: {
        reportCode: report.reportId,
        fightId: report.fights![0].fightId,
        sourceId: index + 7,
        playerName: `Player ${index + 1}`,
        duration: report.duration,
        isComplete: true,
        spells: [],
        events: []
      }
    }));
    return report;
  });
  const { container } = render(CompRankingsResults, {
    rankings,
    edition: 'anniversary',
    worldData: {}
  });
  await waitFor(() => expect(getCompRankingDetails).toHaveBeenCalledTimes(3));
  const row = container.querySelector('article')!;
  const details = row.querySelector('details')!;
  details.open = true;
  await fireEvent(details, new Event('toggle'));
  await waitFor(() =>
    expect(
      within(row).getByRole('region', { name: 'Cooldown comparison timelines' })
    ).toBeInTheDocument()
  );
  await waitFor(() =>
    expect(within(row).queryAllByRole('button', { name: /Load report/ })).toHaveLength(0)
  );
  expect(getReportTimeline).not.toHaveBeenCalled();
  for (const name of ['Tanks', 'Healers', 'DPS']) {
    expect(within(row).getByRole('heading', { name })).toBeInTheDocument();
    const group = within(row).getByRole('region', { name });
    expect(group.querySelector('.role-timelines .role-group')).toBeNull();
    expect(group.querySelector('.role-group')?.nextElementSibling).toHaveClass('role-timelines');
    expect(group.querySelector('.axis-row')).toBeInTheDocument();
    expect(group.querySelectorAll('.player-row')).toHaveLength(name === 'DPS' ? 3 : 1);
  }
  expect(within(row).getByText('0 (Top 7%)')).toBeInTheDocument();
  expect(within(row).queryAllByRole('button', { name: 'Use as boss reference' })).toHaveLength(0);
});

it('retains all 50 summary rows, inspects exactly top three, and loads only the clicked remaining kill', async () => {
  const { container } = render(CompRankingsResults, {
    rankings,
    edition: 'anniversary',
    worldData: {}
  });
  await waitFor(() => expect(getCompRankingDetails).toHaveBeenCalledTimes(3));
  expect(container.querySelectorAll('article')).toHaveLength(50);
  expect(screen.queryByRole('button', { name: /Load next/ })).not.toBeInTheDocument();
  expect(vi.mocked(getCompRankingDetails).mock.calls.map(([report]) => report.reportId)).toEqual([
    'Report1',
    'Report2',
    'Report3'
  ]);
  const row = container.querySelectorAll('article')[46];
  await fireEvent.click(
    within(row as HTMLElement).getByRole('button', { name: 'Load kill details' })
  );
  await waitFor(() => expect(getCompRankingDetails).toHaveBeenCalledTimes(4));
  expect(vi.mocked(getCompRankingDetails).mock.calls[3][0].reportId).toBe('Report47');
  await waitFor(() =>
    expect(within(row as HTMLElement).getByText(/Roster & cooldown timelines/)).toBeInTheDocument()
  );
  expect(container.querySelectorAll('article')).toHaveLength(50);
});

it('never backfills top-three nonmatches or excludes pending kills; an individually inspected later match remains visible', async () => {
  vi.mocked(getCompRankingDetails).mockImplementation(async (summary) =>
    detail(summary, summary.reportId === 'Report50' ? 2 : 1)
  );
  const { container } = render(CompRankingsResults, {
    rankings,
    edition: 'anniversary',
    worldData: {},
    filters: { roles: ['heal.eq.2'], specs: ['holy-paladin.gte.2'], minSeconds: 0, maxSeconds: 0 }
  });
  await waitFor(() => expect(getCompRankingDetails).toHaveBeenCalledTimes(3));
  await waitFor(() => expect(container.querySelectorAll('article')).toHaveLength(47));
  expect(container.querySelectorAll('article')).toHaveLength(47);
  expect(screen.queryByText(/No inspected kills match/)).not.toBeInTheDocument();
  const last = container.querySelectorAll('article')[46];
  expect(last).toHaveTextContent('Kill 50');
  await fireEvent.click(
    within(last as HTMLElement).getByRole('button', { name: 'Load kill details' })
  );
  await within(last as HTMLElement).findByText('Roster & cooldown timelines (1 players)');
  expect(getCompRankingDetails).toHaveBeenCalledTimes(4);
  expect(last).toHaveTextContent('Roster & cooldown timelines');
});

it('pauses immediately on a rate limit and retains the failed row with a retry action', async () => {
  vi.mocked(getCompRankingDetails).mockRejectedValueOnce(
    new Error('Warcraft Logs rate limit reached')
  );
  const { container } = render(CompRankingsResults, {
    rankings,
    edition: 'anniversary',
    worldData: {}
  });
  await screen.findByText('Warcraft Logs rate limit reached');
  expect(getCompRankingDetails).toHaveBeenCalledTimes(1);
  expect(container.querySelectorAll('article')).toHaveLength(50);
  await fireEvent.click(screen.getByRole('button', { name: 'Retry kill details' }));
  await waitFor(() => expect(getCompRankingDetails).toHaveBeenCalledTimes(2));
  await screen.findByText('Roster & cooldown timelines (1 players)');
  expect(getCompRankingDetails).toHaveBeenCalledTimes(2);
});

it('aborts in-flight details on unmount and never launches the remaining initial requests', async () => {
  let resolve!: (_report: RankingReport) => void;
  vi.mocked(getCompRankingDetails).mockImplementationOnce(
    () =>
      new Promise((done) => {
        resolve = done;
      })
  );
  const { unmount } = render(CompRankingsResults, {
    rankings,
    edition: 'anniversary',
    worldData: {}
  });
  await waitFor(() => expect(getCompRankingDetails).toHaveBeenCalledTimes(1));
  const signal = vi.mocked(getCompRankingDetails).mock.calls[0][3];
  await unmount();
  expect(signal?.aborted).toBe(true);
  resolve(detail(summaries[0]));
  await Promise.resolve();
  expect(getCompRankingDetails).toHaveBeenCalledTimes(1);
});

it('forwards refresh only for the requested initial and individual detail requests', async () => {
  render(CompRankingsResults, { rankings, edition: 'anniversary', worldData: {}, refresh: true });
  await waitFor(() => expect(getCompRankingDetails).toHaveBeenCalledTimes(3));
  for (const [, edition, refresh, signal] of vi.mocked(getCompRankingDetails).mock.calls) {
    expect(edition).toBe('anniversary');
    expect(refresh).toBe(true);
    expect(signal).toBeInstanceOf(AbortSignal);
  }
});
