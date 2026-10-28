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
  it('filters locally without a report-count summary and resets', async () => {
    render(RankingsList, { rankings, edition: 'anniversary' });
    expect(screen.queryByText('Sample details')).not.toBeInTheDocument();
    expect(screen.queryByRole('group', { name: 'Rankings view' })).not.toBeInTheDocument();
    expect(
      screen.getByRole('region', { name: 'Cooldown comparison timelines' })
    ).toBeInTheDocument();
    expect(screen.queryByText('2 reports')).not.toBeInTheDocument();
    expect(screen.queryByText(/Best DPS|Median kill time/)).not.toBeInTheDocument();
    const filters = screen.getByText('Filters').closest('details')!;
    expect(filters).not.toHaveAttribute('open');
    expect(screen.getByRole('link', { name: 'Alice' })).toHaveAttribute(
      'href',
      'https://fresh.warcraftlogs.com/reports/ReportA'
    );
    await fireEvent.click(screen.getByRole('button', { name: 'Region EU' }));
    expect(screen.queryByText('Alice')).not.toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
    expect(screen.queryByText('1 of 2 reports')).not.toBeInTheDocument();
    expect(screen.getByText('Filters (1 active)')).toBeInTheDocument();
    await fireEvent.click(screen.getByText('Filters (1 active)'));
    expect(filters).toHaveAttribute('open');
    await fireEvent.click(screen.getByRole('button', { name: 'Reset filters' }));
    expect(screen.getByText('Alice')).toBeInTheDocument();
  });

  it('keeps date and sort choices when filters are collapsed and resets their active count', async () => {
    render(RankingsList, { rankings });
    await fireEvent.click(screen.getByText('Filters'));
    await fireEvent.input(screen.getByLabelText('From date'), { target: { value: '2026-10-22' } });
    await fireEvent.change(screen.getByLabelText('Sort by'), { target: { value: 'date' } });
    expect(screen.getByText('Filters (2 active)')).toBeInTheDocument();
    expect(screen.queryByText('Alice')).not.toBeInTheDocument();
    await fireEvent.click(screen.getByText('Filters (2 active)'));
    expect(screen.getByLabelText('From date')).toHaveValue('2026-10-22');
    await fireEvent.click(screen.getByText('Filters (2 active)'));
    await fireEvent.click(screen.getByRole('button', { name: 'Reset filters' }));
    expect(screen.getByText('Filters')).toBeInTheDocument();
    expect(screen.getByText('Alice')).toBeInTheDocument();
  });

  it('shows ten reports at a time and resets pagination for filters, sorting, and new data', async () => {
    const manyRankings: SpecRankingData = {
      ...rankings,
      reports: Array.from({ length: 25 }, (_, index) => ({
        ...rankings.reports[index % 2],
        reportId: `Report${index}`,
        players: [{ ...rankings.reports[index % 2].players[0], name: `Player${index + 1}` }]
      }))
    };
    const { container, rerender } = render(RankingsList, { rankings: manyRankings });
    const rowCount = () => container.querySelectorAll('.player-row').length;
    expect(rowCount()).toBe(10);
    expect(screen.queryByText('25 reports')).not.toBeInTheDocument();
    const more = screen.getByRole('button', { name: 'Load more' });
    expect(container.querySelector('.workspace')?.nextElementSibling).toBe(more);
    await fireEvent.click(more);
    expect(rowCount()).toBe(20);
    await fireEvent.click(more);
    expect(rowCount()).toBe(25);
    expect(screen.queryByRole('button', { name: 'Load more' })).not.toBeInTheDocument();
    await fireEvent.click(screen.getByRole('button', { name: 'Region EU' }));
    expect(rowCount()).toBe(10);
    expect(container.querySelector('.player-row .rank')).toHaveTextContent('2.');
    await fireEvent.click(screen.getByRole('button', { name: 'Load more' }));
    expect(rowCount()).toBe(12);
    await fireEvent.click(screen.getByText('Filters (1 active)'));
    await fireEvent.change(screen.getByLabelText('Sort by'), { target: { value: 'date' } });
    expect(rowCount()).toBe(10);
    await fireEvent.click(screen.getByRole('button', { name: 'Load more' }));
    await rerender({ rankings: { ...manyRankings, updated: '2026-10-23T12:00:00Z' } });
    expect(rowCount()).toBe(10);
    await fireEvent.click(screen.getByRole('button', { name: 'Reset filters' }));
    expect(rowCount()).toBe(10);
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
