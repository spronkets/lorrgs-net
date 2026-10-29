import * as WorldDataApi from '../lib/worldDataApi';
import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import * as API from '../lib/api';
import RotationAnalyzerPage from './RotationAnalyzerPage.svelte';

vi.mock('../lib/api', () => ({
  lookupReport: vi.fn(),
  analyzeRotation: vi.fn()
}));

vi.mock('../lib/worldDataApi', () => ({ getSpell: vi.fn() }));

beforeEach(() => {
  vi.mocked(API.analyzeRotation).mockResolvedValue({ notes: [], abilitySummary: [] });
  window.history.replaceState(
    {},
    '',
    '?page=rotation&reportCode=ReportA&fightId=3&playerName=Alice&autoAnalyze=1'
  );
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.clearAllMocks();
  window.history.replaceState({}, '', '/');
});

function lookup(actors: { id: number; name: string }[]) {
  vi.mocked(API.lookupReport).mockResolvedValue({
    reportData: { report: { fights: [{ id: 3, name: 'Boss' }], masterData: { actors } } }
  });
}

describe('ranking to rotation player selection', () => {
  it('analyzes the named ranking player, not the first actor in the report', async () => {
    lookup([
      { id: 1, name: 'SomeoneElse' },
      { id: 8, name: 'Alice' }
    ]);
    render(RotationAnalyzerPage);
    await waitFor(() =>
      expect(API.analyzeRotation).toHaveBeenCalledWith(
        expect.objectContaining({ reportCode: 'ReportA', fightId: 3, sourceId: 8 })
      )
    );
  });

  it('stops auto-analysis and warns when the player name is ambiguous', async () => {
    lookup([
      { id: 1, name: 'Alice' },
      { id: 8, name: 'Alice' }
    ]);
    const warning = vi.spyOn(console, 'warn').mockImplementation(() => {});
    render(RotationAnalyzerPage);
    await screen.findByText(
      'Could not uniquely resolve Alice in this report. Select the player manually.'
    );
    expect(API.analyzeRotation).not.toHaveBeenCalled();
    expect(warning).toHaveBeenCalled();
  });
});

it('validates manual lookup and analysis inputs, then renders resolved and fallback spell names', async () => {
  window.history.replaceState({}, '', '/');
  const { container } = render(RotationAnalyzerPage);

  await fireEvent.click(screen.getByRole('button', { name: 'Lookup Report' }));
  expect(await screen.findByText('Enter a Warcraft Logs report code first.')).toBeInTheDocument();
  expect(API.lookupReport).not.toHaveBeenCalled();

  await fireEvent.input(screen.getByPlaceholderText('e.g. abcDEF123xyz'), {
    target: { value: '  ReportB  ' }
  });
  vi.mocked(API.lookupReport).mockResolvedValue({
    reportData: {
      report: {
        fights: [
          { id: 3, name: 'Boss' },
          { id: 4, name: 'Second Boss' }
        ],
        masterData: { actors: [{ id: 8, name: 'Alice', subType: 'Paladin' }] }
      }
    }
  });
  await fireEvent.click(screen.getByRole('button', { name: 'Lookup Report' }));
  expect(await screen.findByRole('option', { name: 'Second Boss (ID 4)' })).toBeInTheDocument();
  expect(API.lookupReport).toHaveBeenCalledWith('ReportB');

  await fireEvent.change(screen.getByLabelText('Player'), { target: { value: '8' } });
  await fireEvent.change(screen.getByLabelText('Fight'), { target: { value: '4' } });
  await fireEvent.input(screen.getByLabelText('Parse Percentile (optional)'), {
    target: { value: '95' }
  });
  await fireEvent.input(screen.getByLabelText('Item Level (optional)'), {
    target: { value: '520' }
  });
  vi.mocked(API.analyzeRotation).mockResolvedValue({
    verdict: 'Good',
    score: 90,
    playerName: 'Alice',
    roleHint: 'Healer',
    specHint: 'Holy',
    fightName: 'Second Boss',
    castsPerMinute: 12,
    notes: ['Strong cooldown coverage'],
    abilitySummary: [
      {
        spellId: 123,
        ability: 'Spell:123',
        castCount: 2,
        sharePercent: 50,
        medianIntervalSeconds: 30
      },
      { ability: 'Spell:456', castCount: 1, sharePercent: 25, medianIntervalSeconds: 0 },
      { ability: 'Auto Attack', castCount: 1, sharePercent: 25, medianIntervalSeconds: null },
      { ability: 42, castCount: 0, sharePercent: 0, medianIntervalSeconds: null }
    ]
  });
  vi.mocked(WorldDataApi.getSpell).mockRejectedValue(new Error('spell unavailable'));

  await fireEvent.click(screen.getByRole('button', { name: 'Analyze Rotation' }));
  await waitFor(() => expect(WorldDataApi.getSpell).toHaveBeenCalledWith(123, 'retail'));
  await waitFor(() => expect(container.querySelector('.table')).toHaveTextContent('Unknown Spell'));
  expect(screen.getByText('Unknown Spell (#123)')).toBeInTheDocument();
  expect(screen.getByText('Unknown Spell (#456)')).toBeInTheDocument();
  expect(screen.getByText('Auto Attack')).toBeInTheDocument();
  expect(screen.getByText('Unknown')).toBeInTheDocument();
  expect(screen.getByText('30.0s')).toBeInTheDocument();
  expect(screen.getAllByText('N/A')).toHaveLength(3);
  expect(screen.getByText('Strong cooldown coverage')).toBeInTheDocument();
  expect(API.analyzeRotation).toHaveBeenCalledWith({
    reportCode: 'ReportB',
    fightId: 4,
    sourceId: 8,
    parsePercentile: 95,
    itemLevel: 520,
    eventLimit: 5000
  });
  expect(WorldDataApi.getSpell).toHaveBeenCalledTimes(2);
  expect(container.querySelector('.table')).toBeInTheDocument();
});

it('handles lookup failures and reports an empty report without attempting analysis', async () => {
  window.history.replaceState({}, '', '/');
  render(RotationAnalyzerPage);
  await fireEvent.input(screen.getByPlaceholderText('e.g. abcDEF123xyz'), {
    target: { value: 'Broken' }
  });
  vi.mocked(API.lookupReport).mockRejectedValueOnce('offline');
  await fireEvent.click(screen.getByRole('button', { name: 'Lookup Report' }));
  expect(await screen.findByText('Failed to lookup report')).toBeInTheDocument();
  expect(screen.queryByLabelText('Fight')).not.toBeInTheDocument();

  vi.mocked(API.lookupReport).mockResolvedValueOnce({});
  await fireEvent.click(screen.getByRole('button', { name: 'Lookup Report' }));
  await fireEvent.click(screen.getByRole('button', { name: 'Analyze Rotation' }));
  expect(await screen.findByText('Select a player to analyze.')).toBeInTheDocument();
  expect(API.analyzeRotation).not.toHaveBeenCalled();
});

it('resolves linked actors by explicit ID, applies query filters, and falls back when spell metadata has no name', async () => {
  window.history.replaceState(
    {},
    '',
    '/?reportCode=ReportC&fightId=404&sourceId=8&parsePercentile=88&itemLevel=515&autoAnalyze=1'
  );
  lookup([
    { id: 1, name: 'Other' },
    { id: 8, name: 'Alice' }
  ]);
  vi.mocked(API.analyzeRotation).mockResolvedValue({
    verdict: 'Review',
    score: 70,
    playerName: 'Alice',
    roleHint: 'Damage',
    specHint: 'Retribution',
    fightName: 'Boss',
    castsPerMinute: 8,
    abilitySummary: [{ spellId: 901, castCount: 1, sharePercent: 100 }]
  });
  vi.mocked(WorldDataApi.getSpell).mockResolvedValue({});

  render(RotationAnalyzerPage);

  expect(await screen.findByText('Unknown Spell (#901)')).toBeInTheDocument();
  expect(API.analyzeRotation).toHaveBeenCalledWith({
    reportCode: 'ReportC',
    fightId: 3,
    sourceId: 8,
    parsePercentile: 88,
    itemLevel: 515,
    eventLimit: 5000
  });
  expect(WorldDataApi.getSpell).toHaveBeenCalledWith(901, 'retail');
});

it('skips auto lookup without a report code and reports an unresolved linked player', async () => {
  window.history.replaceState({}, '', '/?page=rotation');
  render(RotationAnalyzerPage);
  expect(API.lookupReport).not.toHaveBeenCalled();

  window.history.replaceState({}, '', '/?reportCode=ReportD&sourceId=999');
  lookup([{ id: 8, name: 'Alice' }]);
  const warning = vi.spyOn(console, 'warn').mockImplementation(() => {});
  render(RotationAnalyzerPage);
  expect(
    await screen.findByText(
      'The linked player was not found in this report. Select the player manually.'
    )
  ).toBeInTheDocument();
  expect(API.analyzeRotation).not.toHaveBeenCalled();
  expect(warning).toHaveBeenCalled();
});

it('handles API analysis failures and empty ability summaries', async () => {
  window.history.replaceState({}, '', '/');
  lookup([{ id: 8, name: 'Alice' }]);
  render(RotationAnalyzerPage);
  await fireEvent.input(screen.getByPlaceholderText('e.g. abcDEF123xyz'), {
    target: { value: 'ReportE' }
  });
  await fireEvent.click(screen.getByRole('button', { name: 'Lookup Report' }));
  await screen.findByLabelText('Player');

  vi.mocked(API.analyzeRotation).mockResolvedValueOnce({
    verdict: 'No abilities',
    abilitySummary: []
  });
  await fireEvent.click(screen.getByRole('button', { name: 'Analyze Rotation' }));
  expect(await screen.findByRole('heading', { name: 'Result: No abilities' })).toBeInTheDocument();
  expect(WorldDataApi.getSpell).not.toHaveBeenCalled();

  vi.mocked(API.analyzeRotation).mockRejectedValueOnce('failed');
  await fireEvent.click(screen.getByRole('button', { name: 'Analyze Rotation' }));
  expect(await screen.findByText('Failed to analyze rotation')).toBeInTheDocument();
});
