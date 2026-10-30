import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';
import RankingsList from './RankingsList.svelte';
import type { SpecRankingData } from '../models/SpecRankingData';

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
  it('renders empty rankings and updates when ranking data arrives', async () => {
    const { container, rerender } = render(RankingsList);
    expect(container.querySelectorAll('.player-row')).toHaveLength(0);
    expect(screen.getByText('No loaded reports match these filters.')).toBeInTheDocument();
    expect(screen.getByLabelText('From date')).toBeVisible();
    await rerender({ rankings: { ...rankings, reports: [] } });
    expect(container.querySelectorAll('.player-row')).toHaveLength(0);
    await rerender({ rankings });
    expect(screen.getByRole('link', { name: 'Alice' })).toBeInTheDocument();
    expect(container.querySelectorAll('.player-row')).toHaveLength(2);
  });

  it('normalizes both kill-time limits on blur and filters by the end date', async () => {
    render(RankingsList, { rankings });
    const minimum = screen.getByLabelText('Minimum kill time');
    const maximum = screen.getByLabelText('Maximum kill time');
    await fireEvent.input(minimum, { target: { value: '4:00' } });
    await fireEvent.blur(minimum);
    await fireEvent.input(maximum, { target: { value: '5:00' } });
    await fireEvent.blur(maximum);
    expect(minimum).toHaveValue('4:00');
    expect(maximum).toHaveValue('5:00');
    expect(screen.getByRole('link', { name: 'Alice' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Bob' })).toBeInTheDocument();

    await fireEvent.input(screen.getByLabelText('To date'), {
      target: { value: '2026-10-21' }
    });
    expect(screen.getByRole('link', { name: 'Alice' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Bob' })).not.toBeInTheDocument();
    await fireEvent.click(screen.getByRole('button', { name: 'Reset filters' }));
    expect(minimum).toHaveValue('');
    expect(maximum).toHaveValue('');
    expect(screen.getByLabelText('To date')).toHaveValue('');
    expect(screen.getByRole('link', { name: 'Bob' })).toBeInTheDocument();
  });

  it('filters locally without a report-count summary and resets', async () => {
    render(RankingsList, { rankings, edition: 'anniversary' });
    expect(screen.queryByText('Sample details')).not.toBeInTheDocument();
    expect(screen.queryByRole('group', { name: 'Rankings view' })).not.toBeInTheDocument();
    expect(
      screen.getByRole('region', { name: 'Cooldown comparison timelines' })
    ).toBeInTheDocument();
    expect(screen.queryByText('2 reports')).not.toBeInTheDocument();
    expect(screen.queryByText(/Best DPS|Median kill time/)).not.toBeInTheDocument();
    expect(screen.queryByText('Filters')).not.toBeInTheDocument();
    for (const label of ['From date', 'To date', 'Sort by']) {
      expect(screen.getByLabelText(label)).toBeVisible();
      expect(screen.getByLabelText(label).closest('details')).toBeNull();
    }
    expect(screen.getByRole('button', { name: 'Reset filters' })).toBeVisible();
    expect(screen.getByRole('link', { name: 'Alice' })).toHaveAttribute(
      'href',
      'https://fresh.warcraftlogs.com/reports/ReportA'
    );
    await fireEvent.click(screen.getByRole('button', { name: 'Region EU' }));
    expect(screen.queryByText('Alice')).not.toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
    expect(screen.queryByText('1 of 2 reports')).not.toBeInTheDocument();
    await fireEvent.click(screen.getByRole('button', { name: 'Reset filters' }));
    expect(screen.getByText('Alice')).toBeInTheDocument();
  });

  it('applies always-visible date and sort choices and resets their values', async () => {
    render(RankingsList, { rankings });
    await fireEvent.input(screen.getByLabelText('From date'), { target: { value: '2026-10-22' } });
    await fireEvent.change(screen.getByLabelText('Sort by'), { target: { value: 'date' } });
    expect(screen.queryByText('Alice')).not.toBeInTheDocument();
    expect(screen.getByLabelText('From date')).toHaveValue('2026-10-22');
    await fireEvent.click(screen.getByRole('button', { name: 'Reset filters' }));
    expect(screen.getByLabelText('From date')).toHaveValue('');
    expect(screen.getByLabelText('Sort by')).toHaveValue('rank');
    expect(screen.getByText('Alice')).toBeInTheDocument();
  });

  it('shows the full ranking response without pagination and filters all reports', async () => {
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
    expect(rowCount()).toBe(25);
    expect(screen.getByText('Player25')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Load more' })).not.toBeInTheDocument();
    await fireEvent.click(screen.getByRole('button', { name: 'Region EU' }));
    expect(rowCount()).toBe(12);
    expect(container.querySelector('.player-row .rank')).toHaveTextContent(/^2$/);
    expect(container.querySelector('.player-row .rank')).toHaveAttribute(
      'aria-label',
      'Position 2'
    );
    await fireEvent.change(screen.getByLabelText('Sort by'), { target: { value: 'date' } });
    expect(rowCount()).toBe(12);
    await rerender({ rankings: { ...manyRankings, updated: '2026-10-23T12:00:00Z' } });
    expect(rowCount()).toBe(12);
    await fireEvent.click(screen.getByRole('button', { name: 'Reset filters' }));
    expect(rowCount()).toBe(25);
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
