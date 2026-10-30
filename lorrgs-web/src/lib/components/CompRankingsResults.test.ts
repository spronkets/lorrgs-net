import { fireEvent, render, screen, waitFor, within } from '@testing-library/svelte';
import { beforeEach, afterEach, expect, it, vi } from 'vitest';
import type { CompRankingData } from '../models/CompRankingData';
import type { RankingReport } from '../models/RankingReport';
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

it('shows deaths only when the count is positive', async () => {
  const reports = [0, 2].map((deaths, index) => ({
    ...summaries[index],
    fights: [{ ...summaries[index].fights![0], deaths }]
  }));
  const { container } = render(CompRankingsResults, {
    rankings: { ...rankings, reports },
    worldData: {},
    edition: 'anniversary'
  });
  await waitFor(() => expect(container.querySelectorAll('article')).toHaveLength(2));
  const stats = container.querySelectorAll('.stats');
  expect(stats[0]).not.toHaveTextContent('deaths');
  expect(stats[1]).toHaveTextContent('(2 deaths)');
});

it('shows summary faction and identity below linked titles without extra requests', async () => {
  const reports = summaries.map((report, index) => ({
    ...report,
    guildName: index === 3 ? 'Summary Guild' : null,
    serverName: index === 3 ? 'Thunderstrike' : null,
    serverRegion: index >= 3 ? 'eu' : null,
    factionId: index === 3 ? 1 : index === 4 ? 2 : null
  }));
  const { container } = render(CompRankingsResults, {
    rankings: { ...rankings, reports },
    worldData: {},
    edition: 'anniversary'
  });
  await waitFor(() => expect(container.querySelectorAll('article')).toHaveLength(50));
  const headings = container.querySelectorAll('article h3');
  expect(headings[3]).toHaveTextContent('Summary Guild');
  expect(headings[4]).toHaveTextContent('Unknown guild');
  expect(headings[3].querySelector('.report-name')).toBeNull();
  expect(headings[3]).not.toHaveTextContent('Thunderstrike');
  expect(within(headings[3] as HTMLElement).getByRole('link')).toHaveTextContent('Summary Guild');
  expect(headings[3].querySelector('a svg')).toHaveAttribute('width', '11');
  const rows = container.querySelectorAll('article');
  expect(rows[3].querySelector('.raid-identity')).toHaveTextContent('EU Thunderstrike');
  expect(within(rows[3] as HTMLElement).getByAltText('Alliance')).toHaveAttribute(
    'src',
    '/images/wow/shared/factions/alliance.png'
  );
  expect(within(rows[4] as HTMLElement).getByAltText('Horde')).toBeInTheDocument();
  expect(rows[5].querySelector('.faction-icon')).toBeNull();
  expect(within(headings[3] as HTMLElement).getByRole('link')).toHaveAttribute('target', '_blank');
  expect(getCompRankingDetails).toHaveBeenCalledTimes(3);
});

it('shows upstream role counts for uninspected rows without extra detail requests', async () => {
  const reports = summaries.map((report) => ({
    ...report,
    fights: report.fights!.map((fight) => ({
      ...fight,
      roleCounts: { tank: 2, heal: 4, mdps: 9, rdps: 10 }
    }))
  }));
  const { container } = render(CompRankingsResults, {
    rankings: { ...rankings, reports },
    worldData: {},
    edition: 'anniversary'
  });
  await waitFor(() => expect(container.querySelectorAll('article')).toHaveLength(50));
  const row = container.querySelectorAll('article')[3];
  for (const label of ['Tanks: 2', 'Healers: 4', 'Melee DPS: 9', 'Ranged DPS: 10'])
    expect(within(row as HTMLElement).getByText(label)).toBeInTheDocument();
  expect(
    within(row as HTMLElement).getByText('Class/spec composition pending · roster not inspected')
  ).toBeInTheDocument();
  expect(getCompRankingDetails).toHaveBeenCalledTimes(3);
});

it('keeps results in a loading state until all three initial reports are ready', async () => {
  const pending = new Map<string, (report: RankingReport) => void>();
  vi.mocked(getCompRankingDetails).mockImplementation(
    (summary) => new Promise((resolve) => pending.set(summary.reportId, resolve))
  );
  const { container } = render(CompRankingsResults, {
    rankings,
    worldData: {},
    edition: 'anniversary'
  });
  expect(screen.getByRole('status')).toHaveTextContent('0 of 3 reports ready');
  expect(screen.getByRole('status').querySelector('.loading-spinner')).toBeInTheDocument();
  expect(container.querySelector('.comp-results')).toHaveAttribute('aria-busy', 'true');
  expect(container.querySelectorAll('article')).toHaveLength(0);
  expect(getCompRankingDetails).toHaveBeenCalledTimes(3);

  for (const [completed, index] of [2, 0, 1].entries()) {
    await waitFor(() => expect(pending.has(summaries[index].reportId)).toBe(true));
    pending.get(summaries[index].reportId)!(detail(summaries[index]));
    if (completed < 2) {
      await waitFor(() =>
        expect(screen.getByRole('status')).toHaveTextContent(`${completed + 1} of 3 reports ready`)
      );
      expect(container.querySelectorAll('article')).toHaveLength(0);
    }
  }
  await waitFor(() => expect(screen.queryByRole('status')).not.toBeInTheDocument());
  expect(container.querySelector('.comp-results')).toHaveAttribute('aria-busy', 'false');
  expect(container.querySelectorAll('article')).toHaveLength(50);
  expect(screen.getAllByText('Roster & cooldown timelines (1 players)')).toHaveLength(3);
  expect(getCompRankingDetails).toHaveBeenCalledTimes(3);
});

it('retains upstream rank positions instead of reordering by loaded report ranks', async () => {
  vi.mocked(getCompRankingDetails).mockImplementation(async (summary) => {
    const report = detail(summary);
    const rank = summary.reportId === 'Report1' ? 8 : 2;
    report.fights![0].execution = { rank, best: rank, totalParses: 100, rankPercent: 99 };
    report.fights![0].speed = { rank: 5, best: 4, totalParses: 100, rankPercent: 95 };
    report.guildName = 'Raid Guild';
    report.serverName = 'Thunderstrike';
    report.serverRegion = 'eu';
    report.factionId = 2;
    return report;
  });
  const { container } = render(CompRankingsResults, {
    rankings,
    worldData: {},
    edition: 'anniversary'
  });
  await waitFor(() => {
    const headings = [...container.querySelectorAll('article > .heading h3')];
    expect(
      headings.slice(0, 4).map((heading) => heading.textContent?.replace(/\s+/g, ' ').trim())
    ).toEqual([
      'Raid Guild (Kill 1)',
      'Raid Guild (Kill 2)',
      'Raid Guild (Kill 3)',
      'Unknown guild'
    ]);
  });
  const first = container.querySelector('article')!;
  expect(first.querySelector('.efficiency-summary')).toHaveTextContent('Efficiency #8 (Top 1%)');
  expect(first.querySelector('h3 .rank')).toBeNull();
  expect(first.querySelector('.raid-identity')).toHaveTextContent('EU Thunderstrike');
  expect(within(first).getByAltText('Horde')).toBeInTheDocument();
  expect(within(first).getByAltText('Horde')).toHaveAttribute('title', 'Horde');
  expect(first.querySelector('.raid-identity')).not.toHaveTextContent('Horde');
  expect(first.querySelector('.guild-name')).toHaveClass('horde');
  expect(first.querySelector('.date-line')).toHaveTextContent('Kill time 3:20 Speed #5 (Top 5%)');
  expect(first.querySelector('.date-line')).not.toHaveTextContent('·');
  expect(first.querySelector('.stats')).toHaveTextContent('1 damage taken');
  expect(first.querySelector('.stats')).not.toHaveTextContent('deaths');
  expect(within(first).queryByText('Execution')).not.toBeInTheDocument();
  expect(first.querySelector('.speed-summary')).toHaveAttribute(
    'title',
    'Speed ranking out of 100 ranked kills'
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
  await waitFor(() => expect(screen.queryByRole('status')).not.toBeInTheDocument());
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

it('keeps upstream positions for filtered rows and existing paged links', async () => {
  const { container } = render(CompRankingsResults, {
    rankings: { ...rankings, page: 2 },
    edition: 'anniversary',
    worldData: {},
    filters: { roles: ['heal.eq.2'], specs: [], minSeconds: 0, maxSeconds: 0 }
  });
  await waitFor(() => expect(container.querySelectorAll('article')).toHaveLength(47));
  const headings = [...container.querySelectorAll('article > .heading h3')];
  expect(headings[0].closest('article')).toHaveAttribute('data-position', '54');
  expect(headings.at(-1)?.closest('article')).toHaveAttribute('data-position', '100');
  expect(container).not.toHaveTextContent('Rank unavailable');
});

it('retains all 50 summary rows, inspects exactly top three, and loads only the clicked remaining kill', async () => {
  const { container } = render(CompRankingsResults, {
    rankings,
    edition: 'anniversary',
    worldData: {}
  });
  await waitFor(() => expect(getCompRankingDetails).toHaveBeenCalledTimes(3));
  await waitFor(() => expect(container.querySelectorAll('article')).toHaveLength(50));
  expect(screen.queryByRole('button', { name: /Load next/ })).not.toBeInTheDocument();
  expect(vi.mocked(getCompRankingDetails).mock.calls.map(([report]) => report.reportId)).toEqual([
    'Report1',
    'Report2',
    'Report3'
  ]);
  const row = container.querySelectorAll('article')[46];
  await fireEvent.click(within(row as HTMLElement).getByRole('button', { name: 'Load report' }));
  await waitFor(() => expect(getCompRankingDetails).toHaveBeenCalledTimes(4));
  expect(vi.mocked(getCompRankingDetails).mock.calls[3][0].reportId).toBe('Report47');
  await waitFor(() =>
    expect(within(row as HTMLElement).getByText(/Roster & cooldown timelines/)).toBeInTheDocument()
  );
  expect(container.querySelectorAll('article')).toHaveLength(50);
});

it('shows a spinner while loading an individual kill and preserves summary raid identity', async () => {
  const summary = {
    ...summaries[0],
    guildName: 'Summary Guild',
    serverName: 'Summary Realm',
    serverRegion: 'eu'
  };
  let resolve!: (report: RankingReport) => void;
  vi.mocked(getCompRankingDetails)
    .mockRejectedValueOnce(new Error('Temporarily unavailable'))
    .mockImplementationOnce(
      () =>
        new Promise((done) => {
          resolve = done;
        })
    );
  const { container } = render(CompRankingsResults, {
    rankings: { ...rankings, reports: [summary] },
    worldData: {},
    edition: 'anniversary'
  });
  await screen.findByText('Temporarily unavailable');
  expect(container.querySelector('.raid-identity')).toHaveTextContent('EU Summary Realm');
  await fireEvent.click(screen.getByRole('button', { name: 'Retry loading report' }));
  const loading = screen.getByRole('button', { name: 'Loading details…' });
  expect(loading).toBeDisabled();
  expect(loading.querySelector('.loading-spinner')).toBeInTheDocument();
  resolve({
    ...detail(summary),
    guildName: undefined,
    serverName: undefined,
    serverRegion: undefined
  });
  await screen.findByText('Roster & cooldown timelines (1 players)');
  expect(container.querySelector('.raid-identity')).toHaveTextContent('EU Summary Realm');
  expect(container.querySelector('.loading-spinner')).toBeNull();
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
  expect(last).toHaveAttribute('data-position', '50');
  expect(last.querySelector('.report-name')).toBeNull();
  await fireEvent.click(within(last as HTMLElement).getByRole('button', { name: 'Load report' }));
  await within(last as HTMLElement).findByText('Roster & cooldown timelines (1 players)');
  expect(getCompRankingDetails).toHaveBeenCalledTimes(4);
  expect(last).toHaveTextContent('Roster & cooldown timelines');
});

it('retains successful concurrent rows when one request is rate limited and retries only the failure', async () => {
  vi.mocked(getCompRankingDetails).mockRejectedValueOnce(
    new Error('Warcraft Logs rate limit reached')
  );
  const { container } = render(CompRankingsResults, {
    rankings,
    edition: 'anniversary',
    worldData: {}
  });
  await screen.findByText('Warcraft Logs rate limit reached');
  expect(getCompRankingDetails).toHaveBeenCalledTimes(3);
  expect(container.querySelectorAll('article')).toHaveLength(50);
  expect(screen.getAllByText('Roster & cooldown timelines (1 players)')).toHaveLength(2);
  await fireEvent.click(screen.getByRole('button', { name: 'Retry loading report' }));
  await waitFor(() => expect(getCompRankingDetails).toHaveBeenCalledTimes(4));
  await waitFor(() =>
    expect(screen.getAllByText('Roster & cooldown timelines (1 players)')).toHaveLength(3)
  );
  expect(getCompRankingDetails).toHaveBeenCalledTimes(4);
});

it('aborts all concurrent initial detail requests on unmount', async () => {
  const pending: ((_report: RankingReport) => void)[] = [];
  vi.mocked(getCompRankingDetails).mockImplementation(
    () =>
      new Promise((done) => {
        pending.push(done);
      })
  );
  const { unmount } = render(CompRankingsResults, {
    rankings,
    edition: 'anniversary',
    worldData: {}
  });
  expect(getCompRankingDetails).toHaveBeenCalledTimes(3);
  await unmount();
  for (const [, , , signal] of vi.mocked(getCompRankingDetails).mock.calls)
    expect(signal?.aborted).toBe(true);
  pending.forEach((resolve, index) => resolve(detail(summaries[index])));
  await Promise.resolve();
  expect(getCompRankingDetails).toHaveBeenCalledTimes(3);
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

it('applies inclusive duration bounds and both composition filters to inspected details', async () => {
  const selectedSummaries = summaries.slice(0, 4).map((summary, index) => ({
    ...summary,
    title: `Filtered kill ${index + 1}`,
    duration: index === 1 ? 180000 : index === 2 ? 220000 : 200000
  }));
  vi.mocked(getCompRankingDetails).mockImplementation(async (summary) => {
    if (summary.reportId === 'Report1' || summary.reportId === 'Report2') {
      return detail(summary);
    }
    return {
      ...detail(summary),
      fights: [
        {
          ...summary.fights![0],
          composition: {
            roles: { tank: 1 },
            specs: { 'holy-paladin': 1 },
            classes: { paladin: 1 }
          }
        }
      ]
    };
  });
  const { container } = render(CompRankingsResults, {
    rankings: { ...rankings, reports: selectedSummaries },
    edition: 'anniversary',
    worldData: {},
    filters: {
      roles: ['heal.eq.1'],
      specs: ['holy-paladin.eq.1'],
      minSeconds: 190,
      maxSeconds: 210
    }
  });

  await waitFor(() => expect(getCompRankingDetails).toHaveBeenCalledTimes(3));
  await waitFor(() => expect(screen.queryByRole('status')).not.toBeInTheDocument());
  const pendingNonmatch = [...container.querySelectorAll('article')].find(
    (article) => article.dataset.position === '4'
  )!;
  await fireEvent.click(
    within(pendingNonmatch as HTMLElement).getByRole('button', { name: 'Load report' })
  );
  await waitFor(() => expect(getCompRankingDetails).toHaveBeenCalledTimes(4));
  await waitFor(() => expect(container.querySelectorAll('article')).toHaveLength(1));
  expect(container.querySelector('article')).toHaveTextContent('Filtered kill 1');
  expect(container.querySelector('article')).not.toHaveTextContent('Filtered kill 4');
});

it('derives roster roles from world data and filters the expanded roster', async () => {
  const summary = summaries[0];
  vi.mocked(getCompRankingDetails).mockImplementation(async (item) => {
    const report = detail(item);
    const makePlayer = (
      name: string,
      specSlug: string,
      parsePercentile?: number,
      roleCode?: string
    ) => ({
      ...report.players![0],
      name,
      specSlug,
      parsePercentile,
      roleCode,
      timeline: {
        reportCode: item.reportId,
        fightId: item.fights![0].fightId,
        sourceId: 7,
        playerName: name,
        duration: item.duration,
        isComplete: true,
        spells: [],
        events: []
      }
    });
    report.players = [
      makePlayer('Zed', 'holy-paladin', 90),
      makePlayer('Ada', 'holy-paladin', 90),
      makePlayer('No role', 'unknown-spec'),
      makePlayer('Tank', 'unknown-spec', 95, 'tank')
    ];
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
    report.fights![0].execution = { rank: 0, best: 0, totalParses: 0, rankPercent: null };
    report.fights![0].composition = {
      roles: { heal: 2, tank: 1 },
      specs: { 'holy-paladin': 2, 'unknown-spec': 2 },
      classes: { paladin: 2 }
    };
    return report;
  });
  const { container } = render(CompRankingsResults, {
    rankings: { ...rankings, reports: [summary] },
    edition: 'anniversary',
    worldData: {
      roles: [
        { id: 1, code: 'tank', name: 'Tanks' },
        { id: 2, code: 'heal', name: 'Healers' }
      ],
      specs: [
        {
          id: 65,
          nameSlug: 'holy',
          fullNameSlug: 'holy-paladin',
          fullName: 'Holy Paladin',
          classId: 2,
          roleId: 2
        }
      ],
      classes: [{ id: 2, nameSlug: 'paladin', color: '#f48cba' }]
    }
  });

  await screen.findByText('Roster & cooldown timelines (4 players)');
  const row = container.querySelector('article')!;
  expect(within(row).getByRole('button', { name: 'All: 4' })).toHaveAttribute(
    'aria-pressed',
    'true'
  );
  expect(row.querySelector('.composition-heading .roster-filters')).not.toBeNull();
  const details = row.querySelector('details')!;
  details.open = true;
  await fireEvent(details, new Event('toggle'));
  await within(row).findByRole('region', { name: 'Cooldown comparison timelines' });
  expect(within(row).getByRole('heading', { name: 'Healers' })).toBeInTheDocument();
  await fireEvent.click(within(row).getByRole('button', { name: /^Healers:/ }));
  expect(within(row).getByRole('button', { name: /^Healers:/ })).toHaveAttribute(
    'aria-pressed',
    'true'
  );
  expect(within(row).getByRole('region', { name: 'Healers' })).toBeInTheDocument();
  expect(within(row).getByRole('region', { name: 'Healers' })).toHaveTextContent('Ada');
  expect(within(row).getByRole('region', { name: 'Healers' })).toHaveTextContent('Zed');
  const badges = row.querySelectorAll('.specs > button');
  expect(badges[0]).not.toHaveClass('dimmed');
  expect(badges[1]).toHaveClass('dimmed');
  await fireEvent.click(within(row).getByRole('button', { name: 'All: 4' }));
  expect(row.querySelector('.specs .dimmed')).toBeNull();
  expect(within(row).getByRole('region', { name: 'Tanks' })).toBeInTheDocument();
  const holy = within(row).getByRole('button', { name: 'Holy Paladin ×2' });
  const unknown = within(row).getByRole('button', { name: 'unknown-spec ×2' });
  await fireEvent.click(holy);
  expect(holy).toHaveAttribute('aria-pressed', 'true');
  expect(within(row).getByRole('button', { name: 'All: 4' })).toHaveAttribute('aria-pressed', 'false');
  expect(within(row).queryByRole('region', { name: 'Tanks' })).not.toBeInTheDocument();
  expect(within(row).getByRole('region', { name: 'Healers' })).toHaveTextContent('Ada');
  expect(unknown).toHaveClass('dimmed');
  await fireEvent.click(unknown);
  expect(holy).toHaveAttribute('aria-pressed', 'false');
  expect(unknown).toHaveAttribute('aria-pressed', 'true');
  expect(within(row).queryByRole('region', { name: 'Healers' })).not.toBeInTheDocument();
  expect(within(row).getByRole('region', { name: 'Tanks' })).toBeInTheDocument();
  await fireEvent.click(unknown);
  expect(within(row).getByRole('region', { name: 'Healers' })).toBeInTheDocument();
  await fireEvent.click(holy);
  await fireEvent.click(within(row).getByRole('button', { name: /^Tanks:/ }));
  expect(holy).toHaveAttribute('aria-pressed', 'false');
  expect(within(row).getByRole('region', { name: 'Tanks' })).toBeInTheDocument();
  await fireEvent.click(holy);
  expect(within(row).getByRole('button', { name: /^Tanks:/ })).toHaveAttribute('aria-pressed', 'false');
  expect(within(row).getByRole('region', { name: 'Healers' })).toBeInTheDocument();
  await fireEvent.click(within(row).getByRole('button', { name: 'All: 4' }));
  expect(holy).toHaveAttribute('aria-pressed', 'false');
  expect(row.querySelector('.specs .dimmed')).toBeNull();
  expect(within(row).getByRole('region', { name: 'Tanks' })).toBeInTheDocument();
});

it('explains when a page has no kills to inspect', async () => {
  render(CompRankingsResults, {
    rankings: { ...rankings, reports: [], totalCount: 0 },
    edition: 'anniversary',
    worldData: {}
  });
  expect(
    await screen.findByText('No inspected kills match these filters on this page.')
  ).toBeVisible();
});
