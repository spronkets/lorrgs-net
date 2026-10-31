import { render, screen, fireEvent, within, waitFor } from '@testing-library/svelte';
import { beforeEach, afterEach, expect, it, vi } from 'vitest';
import ReportsPage from './ReportsPage.svelte';
import { lookupReport, getCompRankingDetails } from '../lib/api';
import { reportMetadata } from '../lib/testing/manualReportFixtures';

vi.mock('../lib/api', () => ({
  lookupReport: vi.fn(),
  getCompRankingDetails: vi.fn(),
  getReportTimeline: vi.fn(),
  getBossTimeline: vi.fn()
}));
beforeEach(() => {
  history.replaceState({}, '', '/?page=reports&edition=Anniversary');
  vi.mocked(lookupReport).mockImplementation(async (code) => reportMetadata(code));
  vi.mocked(getCompRankingDetails).mockImplementation(async (summary) => ({
    ...summary,
    players: [{
      name: 'Player', playerId: 7, classId: 9, specId: 1,
      specSlug: 'affliction-warlock', performance: 0
    }],
    fights: [{
      ...summary.fights![0],
      composition: {
        roles: { rdps: 1 }, specs: { 'affliction-warlock': 1 }, classes: { warlock: 1 }
      }
    }]
  }));
});
afterEach(() => vi.resetAllMocks());

const props = { edition: 'anniversary', selectedVersion: 'Anniversary', worldData: {} };

it('shows one aggregate loading counter for the displayed boss and updates it as pulls finish', async () => {
  const pending = new Map<number, () => void>();
  const details = vi.mocked(getCompRankingDetails).getMockImplementation()!;
  vi.mocked(getCompRankingDetails).mockImplementation((summary, ...args) =>
    new Promise((resolve) => {
      pending.set(summary.fights![0].fightId, () => {
        void details(summary, ...args).then(resolve);
      });
    })
  );
  render(ReportsPage, props);
  await addReports();
  expect(screen.getAllByRole('status')).toHaveLength(1);
  expect(screen.getByRole('status')).toHaveTextContent('0 of 2 pulls ready');
  pending.get(1)!();
  await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('1 of 2 pulls ready'));
  await fireEvent.click(screen.getByRole('button', { name: 'Boss' }));
  await fireEvent.click(screen.getByRole('option', { name: 'Nalorakk' }));
  expect(screen.getByRole('status')).toHaveTextContent('0 of 1 pulls ready');
  pending.get(3)!();
  await waitFor(() => expect(screen.queryByRole('status')).not.toBeInTheDocument());
  await fireEvent.click(screen.getByRole('button', { name: 'Boss' }));
  await fireEvent.click(screen.getByRole('option', { name: "Zul'jin" }));
  expect(screen.getByRole('status')).toHaveTextContent('1 of 2 pulls ready');
  pending.get(2)!();
  await waitFor(() => expect(screen.queryByRole('status')).not.toBeInTheDocument());
});

it('shows guild and report identity once in the main report header, not in pull titles', async () => {
  vi.mocked(lookupReport).mockResolvedValueOnce({
    reportData: { report: {
      ...reportMetadata().reportData.report,
      guild: { name: 'Aftershock', faction: { id: 1 },
        server: { name: 'Thunderstrike', region: { slug: 'eu' } } }
    } }
  });
  render(ReportsPage, props);
  await addReports();
  await screen.findByText('(Raid ReportA)');
  expect(screen.getAllByRole('link', { name: 'Aftershock EU' })).toHaveLength(1);
  expect(screen.getByRole('link', { name: 'Aftershock EU' })).toHaveAttribute(
    'href', 'https://fresh.warcraftlogs.com/reports/ReportA'
  );
  expect(screen.getByText('Thunderstrike')).toBeInTheDocument();
  const pull = screen.getByRole('region', { name: "Zul'jin fight 1 from ReportA" });
  await waitFor(() => expect(pull.querySelector('summary')).not.toBeNull());
  expect(pull.querySelector('summary')).not.toHaveTextContent('Aftershock');
  expect(pull.querySelector('summary')).not.toHaveTextContent('Raid ReportA');
});

async function addReports(value = 'ReportA') {
  await fireEvent.input(screen.getByLabelText('Report codes or URLs'), { target: { value } });
  await fireEvent.click(screen.getByRole('button', { name: 'Load reports' }));
  await screen.findByRole('region', { name: 'Raid ReportA fight selection' });
}

it('immediately loads every kill and wipe across reports and bosses without selecting pulls', async () => {
  render(ReportsPage, props);
  await addReports('ReportA ReportB ReportA');
  await waitFor(() => expect(getCompRankingDetails).toHaveBeenCalledTimes(6));
  expect(lookupReport).toHaveBeenCalledTimes(2);
  expect(screen.getByRole('button', { name: 'Remove report ReportA' })).toHaveTextContent(/^Remove report$/);
  expect(screen.getByRole('button', { name: 'Remove report ReportA' })).toHaveAttribute('data-tone', 'danger');
  expect(screen.getByRole('button', { name: 'Remove report ReportA' })).toHaveAttribute('data-outline', 'true');
  expect(screen.queryByRole('checkbox')).not.toBeInTheDocument();
  expect(screen.queryByRole('button', { name: /Load selected pulls/ })).not.toBeInTheDocument();
  expect(vi.mocked(getCompRankingDetails).mock.calls.map(([report]) =>
    `${report.reportId}:${report.fights![0].fightId}`
  )).toEqual(expect.arrayContaining([
    'ReportA:1', 'ReportA:2', 'ReportA:3', 'ReportB:1', 'ReportB:2', 'ReportB:3'
  ]));
  const first = screen.getByRole('region', { name: 'Raid ReportA fight selection' });
  await fireEvent.click(within(first).getByRole('button', { name: 'Boss' }));
  await fireEvent.click(within(first).getByRole('option', { name: 'Nalorakk' }));
  expect(await screen.findByRole('region', { name: 'Nalorakk fight 3 from ReportA' })).toBeVisible();
  expect(screen.queryByRole('region', { name: "Zul'jin fight 1 from ReportA" })).not.toBeInTheDocument();
  expect(getCompRankingDetails).toHaveBeenCalledTimes(6);
  await fireEvent.click(screen.getByRole('button', { name: 'Remove report ReportA' }));
  expect(new URLSearchParams(location.search).getAll('report')).toEqual(['ReportB:10']);
  await fireEvent.click(screen.getByRole('button', { name: 'Remove report ReportB' }));
  expect(new URLSearchParams(location.search).getAll('report')).toEqual([]);
});

it('restores entire reports and boss focus from the URL and removes legacy pull parameters', async () => {
  history.replaceState({}, '', '/?page=reports&edition=Anniversary&report=ReportA&reportBoss=ReportA:11&pull=ReportA:1&loadedPull=ReportA:1');
  const { unmount } = render(ReportsPage, props);
  await waitFor(() => expect(getCompRankingDetails).toHaveBeenCalledTimes(3));
  expect(await screen.findByRole('region', { name: 'Nalorakk fight 3 from ReportA' })).toBeVisible();
  const params = new URLSearchParams(location.search);
  expect(params.get('edition')).toBe('Anniversary');
  expect(params.getAll('report')).toEqual(['ReportA:11']);
  expect(params.has('reportBoss')).toBe(false);
  expect(params.has('pull')).toBe(false);
  expect(params.has('loadedPull')).toBe(false);
  unmount();
  render(ReportsPage, props);
  await waitFor(() => expect(getCompRankingDetails).toHaveBeenCalledTimes(6));
  expect(await screen.findByRole('region', { name: 'Nalorakk fight 3 from ReportA' })).toBeVisible();
});

it('restores independent bosses from compact multi-report links and updates them without duplicating codes', async () => {
  history.replaceState({}, '', '/?page=reports&edition=Anniversary&report=ReportA:11&report=ReportB:10');
  render(ReportsPage, props);
  expect(await screen.findByRole('region', { name: 'Nalorakk fight 3 from ReportA' })).toBeVisible();
  const second = await screen.findByRole('region', { name: 'Raid ReportB fight selection' });
  await fireEvent.click(within(second).getByRole('button', { name: 'Boss' }));
  await fireEvent.click(within(second).getByRole('option', { name: 'Nalorakk' }));
  expect(new URLSearchParams(location.search).getAll('report')).toEqual(['ReportA:11', 'ReportB:11']);
  expect(new URLSearchParams(location.search).has('reportBoss')).toBe(false);
  expect(lookupReport).toHaveBeenNthCalledWith(1, 'ReportA', expect.any(AbortSignal));
  expect(lookupReport).toHaveBeenNthCalledWith(2, 'ReportB', expect.any(AbortSignal));
});

it('keeps each pull independently collapsible with the composition-ranking header', async () => {
  render(ReportsPage, props);
  await addReports();
  const kill = screen.getByRole('region', { name: "Zul'jin fight 1 from ReportA" });
  const wipe = screen.getByRole('region', { name: "Zul'jin fight 2 from ReportA" });
  await waitFor(() => {
    expect(kill.querySelector('details')).not.toBeNull();
    expect(wipe.querySelector('details')).not.toBeNull();
  });
  const accordion = kill.querySelector('details')!;
  const other = wipe.querySelector('details')!;
  expect(accordion.open).toBe(false);
  expect(other.open).toBe(false);
  expect(accordion.querySelector('summary')).toHaveAccessibleName('Kill #1');
  expect(other.querySelector('summary')).toHaveAccessibleName('Wipe #2');
  expect(accordion.querySelector('summary')).not.toHaveTextContent("Zul'jin");
  expect(accordion.querySelector('summary img')).toBeNull();
  expect(within(kill).getByRole('group', { name: 'Roster roles' })).not.toBeVisible();
  accordion.open = true;
  await fireEvent(accordion, new Event('toggle'));
  expect(within(kill).getByRole('group', { name: 'Roster roles' })).toBeVisible();
  expect(other.open).toBe(false);
  expect(getCompRankingDetails).toHaveBeenCalledTimes(3);
});

it('resolves boss portraits by name when encounter IDs differ from world data', async () => {
  render(ReportsPage, {
    ...props, worldData: {
      bosses: [{ id: 999, name: "Zul'jin", nameSlug: 'zuljin', raidId: 1, icon: 'zuljin' }]
    }
  });
  await addReports();
  const trigger = screen.getByRole('button', { name: 'Boss' });
  expect(trigger.querySelector('img')).toHaveAttribute(
    'src', 'https://wow.zamimg.com/images/wow/icons/medium/zuljin.jpg'
  );
  await fireEvent.click(trigger);
  expect(screen.getByRole('option', { name: 'Nalorakk' }).querySelector('img')).toHaveAttribute(
    'src', 'https://wow.zamimg.com/images/wow/journal/ui-ej-boss-nalorakk.png'
  );
});

it('validates input and retries individual failures without reloading successful reports', async () => {
  render(ReportsPage, props);
  await fireEvent.click(screen.getByRole('button', { name: 'Load reports' }));
  expect(screen.getByRole('alert')).toHaveTextContent('Enter at least one');
  vi.mocked(lookupReport).mockRejectedValueOnce(new Error('Report unavailable'));
  await addReports('Missing ReportA');
  expect(screen.getByRole('alert')).toHaveTextContent('Missing: Report unavailable');
  expect(new URLSearchParams(location.search).getAll('report')).toContain('Missing');
  await fireEvent.click(screen.getByRole('button', { name: 'Load reports' }));
  await screen.findByRole('region', { name: 'Raid Missing fight selection' });
  await waitFor(() => expect(getCompRankingDetails).toHaveBeenCalledTimes(6));
  expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  expect(lookupReport).toHaveBeenCalledTimes(3);
});

it('shows loading and cancels outstanding lookup on unmount', async () => {
  let resolve!: (_value: unknown) => void;
  vi.mocked(lookupReport).mockImplementationOnce(() => new Promise((done) => { resolve = done; }));
  const { unmount } = render(ReportsPage, props);
  await fireEvent.input(screen.getByLabelText('Report codes or URLs'), { target: { value: 'ReportA' } });
  await fireEvent.click(screen.getByRole('button', { name: 'Load reports' }));
  expect(screen.getByRole('status')).toHaveTextContent('Loading report overviews');
  const signal = vi.mocked(lookupReport).mock.calls[0][1]!;
  unmount();
  expect(signal.aborted).toBe(true);
  resolve(reportMetadata());
});

it('handles reports without boss fights or raid metadata', async () => {
  vi.mocked(lookupReport).mockResolvedValueOnce({
    reportData: { report: { ...reportMetadata().reportData.report, zone: null, fights: [] } }
  });
  render(ReportsPage, props);
  await addReports();
  expect(screen.getByText('No boss pulls were found in this report.')).toBeInTheDocument();
  expect(screen.getByText('Unknown raid')).toBeInTheDocument();
  expect(getCompRankingDetails).not.toHaveBeenCalled();
});

it('surfaces non-Error failures and cancels failed lookups after unmount', async () => {
  vi.mocked(lookupReport).mockRejectedValueOnce('network unavailable');
  const { unmount } = render(ReportsPage, props);
  await addReports('Failed ReportA');
  expect(screen.getByRole('alert')).toHaveTextContent('Failed: Unable to load report.');
  let reject!: (_cause: Error) => void;
  vi.mocked(lookupReport).mockImplementationOnce(() => new Promise((_done, fail) => { reject = fail; }));
  await fireEvent.click(screen.getByRole('button', { name: 'Load reports' }));
  unmount();
  reject(new Error('Aborted'));
});

it('requires an edition before requests are allowed', () => {
  render(ReportsPage, { edition: '', worldData: {} });
  expect(screen.getByRole('button', { name: 'Load reports' })).toBeDisabled();
  expect(screen.getByText('Select an edition before loading reports.')).toBeInTheDocument();
});
