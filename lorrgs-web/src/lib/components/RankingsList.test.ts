import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';
import RankingsList from './RankingsList.svelte';
import type { SpecRankingData } from '../api';

const rankings: SpecRankingData = {
  specSlug: 'retribution-paladin',
  bossSlug: 'lady-vashj',
  difficulty: 'normal',
  metric: 'dps',
  updated: '2026-10-22T12:00:00Z',
  isDirty: false,
  percentile: null,
  reports: [
    {
      reportId: 'ReportA',
      title: 'First report',
      duration: 240000,
      percentile: 99,
      startTime: '2026-10-21T12:00:00Z',
      players: [
        {
          playerId: 0,
          name: 'Alice',
          classId: 2,
          specId: 70,
          specSlug: 'retribution-paladin',
          performance: 100,
          serverRegion: 'NA'
        }
      ]
    },
    {
      reportId: 'ReportB',
      title: 'Second report',
      duration: 300000,
      percentile: 98,
      startTime: '2026-10-22T12:00:00Z',
      players: [
        {
          playerId: 0,
          name: 'Bob',
          classId: 2,
          specId: 70,
          specSlug: 'retribution-paladin',
          performance: 0,
          serverRegion: 'EU'
        }
      ]
    }
  ]
};

describe('ranking analytics', () => {
  it('filters locally, updates the sample summary, and resets', async () => {
    render(RankingsList, { rankings, edition: 'anniversary' });
    expect(screen.queryByText('Sample details')).not.toBeInTheDocument();
    expect(screen.queryByRole('group', { name: 'Rankings view' })).not.toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Cooldown comparison timelines' })).toBeInTheDocument();
    expect(screen.getByText('Showing 2 of 2 loaded reports')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Alice' })).toHaveAttribute(
      'href',
      'https://fresh.warcraftlogs.com/reports/ReportA'
    );
    await fireEvent.click(screen.getByRole('button', { name: 'Region EU' }));
    expect(screen.queryByText('Alice')).not.toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
    expect(screen.getByText('Showing 1 of 2 loaded reports')).toBeInTheDocument();
    expect(screen.getByText('Best DPS: 0')).toBeInTheDocument();
    await fireEvent.click(screen.getByRole('button', { name: 'Reset filters' }));
    expect(screen.getByText('Alice')).toBeInTheDocument();
  });

  it('shows invalid-input feedback instead of silently ignoring malformed filters', async () => {
    render(RankingsList, { rankings });
    await fireEvent.input(screen.getByLabelText('Minimum kill time'), {
      target: { value: '4:99' }
    });
    expect(screen.getByRole('alert')).toHaveTextContent('Enter kill times');
    expect(screen.getByText('No loaded reports match these filters.')).toBeInTheDocument();
  });
});
