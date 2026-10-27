import { render, screen, waitFor } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import * as API from '../api';
import RotationAnalyzerPage from './RotationAnalyzerPage.svelte';

vi.mock('../api', () => ({
  lookupReport: vi.fn(),
  analyzeRotation: vi.fn(),
  getSpell: vi.fn()
}));

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
