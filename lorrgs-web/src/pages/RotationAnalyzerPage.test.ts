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
  await fireEvent.click(await screen.findByRole('button', { name: 'Fight' }));
  const secondBossOption = await screen.findByRole('option', { name: 'Second Boss (ID 4)' });
  expect(secondBossOption).toBeInTheDocument();
  await fireEvent.click(secondBossOption);
  expect(API.lookupReport).toHaveBeenCalledWith('ReportB');

  await fireEvent.change(screen.getByLabelText('Player'), { target: { value: '8' } });
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
  await waitFor(() => expect(container.querySelector('.table')).toHaveTextContent('Spell 123'));
  expect(screen.getByText('Spell 123 (#123)')).toBeInTheDocument();
  expect(screen.getByText('Spell 456 (#456)')).toBeInTheDocument();
  expect(screen.getByText('Auto Attack')).toBeInTheDocument();
  expect(screen.getByText('Unknown')).toBeInTheDocument();
  expect(screen.getByText('30.0s')).toBeInTheDocument();
  expect(screen.getAllByText('N/A')).toHaveLength(3);
  expect(screen.getByText('Strong cooldown coverage')).toBeInTheDocument();
  expect(API.analyzeRotation).toHaveBeenCalledWith({
    reportCode: 'ReportB',
    fightId: 4,
    sourceId: 8,
    parsePercentile: null,
    itemLevel: null,
    autoResolveParsePercentile: true,
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
  vi.mocked(WorldDataApi.getSpell).mockResolvedValue({ id: 901, name: '', icon: '', type: '' });

  render(RotationAnalyzerPage);

  expect(await screen.findByText(/#901/)).toBeInTheDocument();
  expect(API.analyzeRotation).toHaveBeenCalledWith({
    reportCode: 'ReportC',
    fightId: 3,
    sourceId: 8,
    parsePercentile: 88,
    itemLevel: 515,
    autoResolveParsePercentile: true,
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

it('renders phase-aware rotation summaries with per-phase cooldown windows', async () => {
  window.history.replaceState({}, '', '/');
  lookup([{ id: 8, name: 'Alice' }]);
  render(RotationAnalyzerPage);
  await fireEvent.input(screen.getByPlaceholderText('e.g. abcDEF123xyz'), {
    target: { value: 'ReportF' }
  });
  await fireEvent.click(screen.getByRole('button', { name: 'Lookup Report' }));
  await screen.findByLabelText('Player');

  vi.mocked(API.analyzeRotation).mockResolvedValueOnce({
    verdict: 'optimal',
    score: 88,
    playerName: 'Alice',
    roleHint: 'dps',
    specHint: 'Paladin',
    fightName: 'Boss',
    castsPerMinute: 35,
    notes: ['Phase-aware rotation summary generated across 2 phases.'],
    abilitySummary: [],
    damageSummary: [
      { spellId: 31884, ability: 'Spell:31884', totalDamage: 125000, hitCount: 2, sharePercent: 18.4 }
    ],
    phaseSummaries: [
      {
        phaseId: 1,
        startSeconds: 0,
        endSeconds: 40,
        durationSeconds: 40,
        castCount: 25,
        castsPerMinute: 37.5,
        rotationPattern: 'spell #20271 → spell #20375 → wait → spell #35395',
        topAbilities: [{ spellId: 20271, castCount: 8, sharePercent: 32 }],
        cooldownSpellIds: [31884]
      },
      {
        phaseId: 2,
        startSeconds: 40,
        endSeconds: 80,
        durationSeconds: 40,
        castCount: 20,
        castsPerMinute: 30,
        rotationPattern: 'spell #20271 → spell #20375 → spell #31892',
        topAbilities: [{ spellId: 31892, castCount: 10, sharePercent: 50 }],
        cooldownSpellIds: []
      }
    ]
  });
  vi.mocked(WorldDataApi.getSpell).mockResolvedValue({
    name: 'Avenging Wrath',
    icon: 'spell_holy_avenginewrath',
    type: ''
  });

  await fireEvent.click(screen.getByRole('button', { name: 'Analyze Rotation' }));
  expect(await screen.findByText('Phase-aware Rotation')).toBeInTheDocument();
  expect(screen.getByText('P1')).toBeInTheDocument();
  expect(screen.getByText('P2')).toBeInTheDocument();
  expect(screen.getByText('spell #20271 → spell #20375 → wait → spell #35395')).toBeInTheDocument();
  expect(screen.getByText('None detected')).toBeInTheDocument();
  expect(screen.getByText('Top Damage Contribution')).toBeInTheDocument();
  expect(screen.getAllByText(/#31884/).length).toBeGreaterThan(0);
});
