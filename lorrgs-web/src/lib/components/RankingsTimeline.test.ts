import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { getReportTimeline, type ReportTimeline } from '../api';
import { rows, response } from '../testing/timelineFixtures';
import RankingsTimeline from './RankingsTimeline.svelte';
import { TIMELINE_ZOOM_LEVELS } from '../timelineUtils';

vi.mock('../api', () => ({ getReportTimeline: vi.fn() }));
const fetchTimeline = vi.mocked(getReportTimeline);
beforeEach(() => {
  fetchTimeline.mockReset();
  fetchTimeline.mockImplementation(async (report) => response(report));
});
afterEach(() => vi.restoreAllMocks());

describe('ranking cooldown comparison', () => {
  it('closes the magnifier at maximum zoom and restores it below the maximum', async () => {
    const maximum = TIMELINE_ZOOM_LEVELS.at(-1);
    const previous = TIMELINE_ZOOM_LEVELS[TIMELINE_ZOOM_LEVELS.length - 2];
    render(RankingsTimeline, { reports: rows.slice(0, 1), edition: 'anniversary' });
    expect(
      Array.from(screen.getByLabelText<HTMLSelectElement>('Zoom').options, (option) =>
        Number(option.value)
      )
    ).toEqual([...TIMELINE_ZOOM_LEVELS]);
    const spell = await screen.findByRole('button', { name: /0:10 · Avenging Wrath · DPS/ });
    await fireEvent.mouseEnter(spell);
    expect(screen.getByRole('tooltip')).toBeInTheDocument();
    await fireEvent.change(screen.getByLabelText('Zoom'), { target: { value: String(maximum) } });
    expect(screen.queryByRole('tooltip')).not.toBeInTheDocument();
    await fireEvent.mouseLeave(spell);
    await fireEvent.mouseEnter(spell);
    expect(screen.queryByRole('tooltip')).not.toBeInTheDocument();
    expect(spell).toHaveClass('spell-highlighted');
    await fireEvent.change(screen.getByLabelText('Zoom'), { target: { value: String(previous) } });
    expect(screen.queryByRole('tooltip')).not.toBeInTheDocument();
    await fireEvent.mouseLeave(spell);
    await fireEvent.mouseEnter(spell);
    expect(screen.getByRole('tooltip')).toHaveTextContent(`${previous * 2}%`);
    expect(fetchTimeline).toHaveBeenCalledTimes(1);
  });

  it('renders every bundled API-cached timeline immediately without timeline requests', async () => {
    const reports = rows.map(({ report, rank }) => ({
      rank,
      report: { ...report, timeline: response(report) }
    }));
    render(RankingsTimeline, { reports, edition: 'anniversary' });
    await waitFor(() =>
      expect(screen.getAllByRole('button', { name: 'Copy note' })).toHaveLength(5)
    );
    expect(screen.getByText('5 / 5 timelines ready')).toBeInTheDocument();
    expect(screen.queryByText('Loading report events…')).not.toBeInTheDocument();
    expect(fetchTimeline).not.toHaveBeenCalled();
  });

  it('uses fixed-size SVG toolbar icons and preserves their accessible toggles', async () => {
    render(RankingsTimeline, { reports: rows.slice(0, 1), edition: 'anniversary' });
    await screen.findByRole('button', { name: 'Copy note' });
    for (const name of ['Spell icons', 'Casts', 'Buff durations', 'Deaths', 'Separate spells']) {
      const toggle = screen.getByRole('checkbox', { name });
      const icon = toggle.nextElementSibling;
      expect(icon).toHaveAttribute('aria-hidden', 'true');
      expect(icon?.querySelector('svg')).toHaveAttribute('viewBox', '0 0 24 24');
      expect(icon?.querySelector('svg')).toHaveAttribute('width', '18');
      const checked = (toggle as HTMLInputElement).checked;
      await fireEvent.click(toggle);
      expect((toggle as HTMLInputElement).checked).toBe(!checked);
    }
    expect(fetchTimeline).toHaveBeenCalledTimes(1);
  });

  it('shows copy feedback in a dismissible toast instead of an inline paragraph', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    const clipboard = Object.getOwnPropertyDescriptor(navigator, 'clipboard');
    Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText } });
    const { container } = render(RankingsTimeline, {
      reports: rows.slice(0, 1),
      edition: 'anniversary'
    });
    await fireEvent.click(await screen.findByRole('button', { name: 'Copy note' }));
    await waitFor(() =>
      expect(
        screen.getByText('Copied selected casts for Player0.').closest('[role="status"]')
      ).not.toBeNull()
    );
    expect(container.querySelector('p[role="status"]')).toBeNull();
    expect(writeText).toHaveBeenCalledTimes(1);
    await fireEvent.click(screen.getByRole('button', { name: 'Dismiss notification' }));
    expect(screen.queryByText('Copied selected casts for Player0.')).not.toBeInTheDocument();
    if (clipboard) Object.defineProperty(navigator, 'clipboard', clipboard);
    else Reflect.deleteProperty(navigator, 'clipboard');
  });

  it('uses only supported roles, filters damage by spec, and keeps shared buffs across newly loaded rows', async () => {
    fetchTimeline.mockImplementation(async (report) => ({
      ...response(report),
      spells: [
        { id: 30451, name: 'Arcane Blast', icon: null },
        { id: 116, name: 'Frostbolt', icon: null },
        { id: 2825, name: 'Bloodlust', icon: null }
      ],
      events: [
        { spellId: 30451, timestamp: 10000, duration: null, kind: 'cast' },
        { spellId: 116, timestamp: 20000, duration: null, kind: 'cast' },
        { spellId: 2825, timestamp: 30000, duration: 40000, kind: 'buff' }
      ]
    }));
    render(RankingsTimeline, { reports: rows, edition: 'anniversary', classId: 8 });
    await screen.findByRole('button', { name: 'Frostbolt' });
    expect(screen.queryByRole('button', { name: 'Tank' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Healing' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Damage' })).not.toBeInTheDocument();
    await fireEvent.click(screen.getByRole('button', { name: 'Arcane' }));
    expect(screen.getByRole('button', { name: 'Arcane Blast' })).toHaveAttribute(
      'aria-pressed',
      'true'
    );
    expect(screen.getByRole('button', { name: 'Frostbolt' })).toHaveAttribute(
      'aria-pressed',
      'false'
    );
    expect(screen.getByRole('button', { name: 'Bloodlust' })).toHaveAttribute(
      'aria-pressed',
      'true'
    );
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Load next 3 timelines' })).toBeEnabled()
    );
    await fireEvent.click(screen.getByRole('button', { name: 'Load next 3 timelines' }));
    await waitFor(() =>
      expect(screen.getAllByRole('button', { name: 'Copy note' })).toHaveLength(5)
    );
    expect(screen.queryByRole('button', { name: /0:20 · Frostbolt/ })).not.toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: /0:10 · Arcane Blast/ })).toHaveLength(5);
    await fireEvent.click(screen.getByRole('button', { name: 'Frost' }));
    expect(screen.getByRole('button', { name: 'Frostbolt' })).toHaveAttribute(
      'aria-pressed',
      'true'
    );
    expect(screen.getByRole('button', { name: 'Arcane Blast' })).toHaveAttribute(
      'aria-pressed',
      'false'
    );
    expect(fetchTimeline).toHaveBeenCalledTimes(5);
  });

  it('shows cast intervals per player only while the spell is highlighted and visible', async () => {
    fetchTimeline.mockImplementation(async (report) => ({
      ...response(report),
      events: [
        ...response(report).events,
        { spellId: 31884, timestamp: 130500, duration: null, kind: 'cast' }
      ]
    }));
    const { container } = render(RankingsTimeline, {
      reports: rows.slice(0, 2),
      edition: 'anniversary'
    });
    const selector = await screen.findByRole('button', { name: 'Avenging Wrath' });
    await waitFor(() =>
      expect(screen.getAllByRole('button', { name: 'Copy note' })).toHaveLength(2)
    );
    expect(screen.queryByText('120.5s')).not.toBeInTheDocument();
    const initialHeight = container.querySelector('.track')?.getAttribute('style');
    await fireEvent.mouseEnter(selector);
    expect(screen.getAllByText('120.5s')).toHaveLength(2);
    expect(container.querySelector('.track')?.getAttribute('style')).toBe(initialHeight);
    expect(container.querySelector('.identity .cast-interval')).toBeNull();
    expect(container.querySelectorAll('.track .cast-interval')).toHaveLength(2);
    expect(
      screen.getByRole('img', {
        name: 'Player0 · Avenging Wrath · 120.5s between casts at 0:10 and 2:10'
      })
    ).toBeInTheDocument();
    await fireEvent.mouseLeave(selector);
    expect(screen.queryByText('120.5s')).not.toBeInTheDocument();
    await fireEvent.focus(selector);
    expect(screen.getAllByText('120.5s')).toHaveLength(2);
    await fireEvent.click(screen.getByRole('checkbox', { name: 'Casts' }));
    expect(screen.queryByText('120.5s')).not.toBeInTheDocument();
    expect(fetchTimeline).toHaveBeenCalledTimes(2);
  });

  it('ends at the longest available fight exactly and shrinks with the filtered sample', async () => {
    fetchTimeline.mockImplementation(async (report) => ({
      ...response(report),
      duration: report.duration
    }));
    const reports = rows.slice(0, 2).map((row, index) => ({
      ...row,
      report: { ...row.report, duration: index === 0 ? 41000 : 301000 }
    }));
    const { container, rerender } = render(RankingsTimeline, {
      reports,
      edition: 'anniversary'
    });
    expect(screen.queryByText('Timeline guide & data limits')).not.toBeInTheDocument();
    await waitFor(() => expect(fetchTimeline).toHaveBeenCalledTimes(2));
    await waitFor(() =>
      expect(container.querySelector('.axis .end-label')).toHaveTextContent('5:01')
    );
    expect(container.querySelector('.timeline-grid')).toHaveStyle({ width: 'calc(100% - 0px)' });
    expect(container.querySelector('.axis')).not.toHaveTextContent('5:30');
    await rerender({ reports: reports.slice(0, 1), edition: 'anniversary' });
    expect(container.querySelector('.timeline-grid')).toHaveStyle({ width: 'calc(100% - 0px)' });
    expect(container.querySelector('.axis .end-label')).toHaveTextContent('0:41');
    expect(fetchTimeline).toHaveBeenCalledTimes(2);
  });

  it('loads the new top three after sorting/filtering and reuses previously loaded rows', async () => {
    const { rerender } = render(RankingsTimeline, { reports: rows, edition: 'anniversary' });
    await waitFor(() => expect(fetchTimeline).toHaveBeenCalledTimes(3));
    await rerender({ reports: [...rows].reverse(), edition: 'anniversary' });
    await waitFor(() => expect(fetchTimeline).toHaveBeenCalledTimes(5));
    expect(fetchTimeline.mock.calls.map(([report]) => report.reportId)).toEqual([
      'Report0',
      'Report1',
      'Report2',
      'Report4',
      'Report3'
    ]);
    await rerender({ reports: rows.slice(0, 3), edition: 'anniversary' });
    expect(fetchTimeline).toHaveBeenCalledTimes(5);
    expect(screen.getByText('3 / 3 timelines ready')).toBeInTheDocument();
  });

  it('links selector hover and keyboard event focus to matching spells across rows', async () => {
    fetchTimeline.mockImplementation(async (report) => ({
      ...response(report),
      spells: [...response(report).spells, { id: 642, name: 'Divine Shield', icon: null }],
      events: [
        ...response(report).events,
        { spellId: 642, timestamp: 20000, duration: 8000, kind: 'buff' }
      ]
    }));
    render(RankingsTimeline, { reports: rows.slice(0, 2), edition: 'anniversary' });
    const selector = await screen.findByRole('button', { name: 'Avenging Wrath' });
    await waitFor(() =>
      expect(screen.getAllByRole('button', { name: /0:20 · Divine Shield/ })).toHaveLength(2)
    );
    await fireEvent.mouseEnter(selector);
    for (const mark of screen.getAllByRole('button', { name: /0:10 · Avenging Wrath/ }))
      expect(mark).toHaveClass('spell-highlighted');
    const shields = screen.getAllByRole('button', { name: /0:20 · Divine Shield/ });
    expect(shields[0]).toHaveClass('spell-dimmed');
    await fireEvent.mouseLeave(selector);
    expect(shields[0]).not.toHaveClass('spell-dimmed');
    await fireEvent.focus(shields[0]);
    expect(
      screen.getByText(/Player0 · Effect on player · buff at 0:20 · 0:08 measured uptime/)
    ).toBeInTheDocument();
    expect(shields[1]).toHaveClass('spell-highlighted');
    await fireEvent.blur(shields[0]);
    expect(shields[1]).not.toHaveClass('spell-highlighted');
    expect(fetchTimeline).toHaveBeenCalledTimes(2);
  });

  it('highlights received Bloodlust in the selector and its measured timeline window', async () => {
    fetchTimeline.mockImplementation(async (report) => ({
      ...response(report),
      spells: [
        ...response(report).spells,
        { id: 2825, name: 'Bloodlust', icon: 'spell_nature_bloodlust.jpg' }
      ],
      events: [
        ...response(report).events,
        { spellId: 2825, timestamp: 30000, duration: 40000, kind: 'buff' }
      ]
    }));
    render(RankingsTimeline, { reports: rows.slice(0, 1), edition: 'anniversary' });
    const bloodlust = await screen.findByRole('button', { name: 'Bloodlust' });
    expect(bloodlust).toHaveClass('power-window');
    expect(screen.getByRole('group', { name: 'DPS / power windows' })).toContainElement(bloodlust);
    const marker = screen.getByRole('button', { name: /0:30 · Bloodlust · 0:40 uptime · DPS/ });
    expect(marker).toHaveClass('buff', 'power-window');
    await fireEvent.click(bloodlust);
    expect(screen.queryByRole('button', { name: /0:30 · Bloodlust/ })).not.toBeInTheDocument();
    expect(fetchTimeline).toHaveBeenCalledTimes(1);
  });

  it('loads the top three rows and loads more only on request', async () => {
    render(RankingsTimeline, { reports: rows, edition: 'anniversary' });
    await waitFor(() => expect(fetchTimeline).toHaveBeenCalledTimes(3));
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Load next 3 timelines' })).toBeEnabled()
    );
    expect(fetchTimeline.mock.calls.every((call) => call[1] === 'anniversary')).toBe(true);
    expect(screen.getAllByRole('button', { name: /0:10 · Avenging Wrath/ })).toHaveLength(6);
    await fireEvent.click(screen.getByRole('button', { name: 'Avenging Wrath' }));
    expect(screen.queryByRole('button', { name: /0:10 · Avenging Wrath/ })).not.toBeInTheDocument();
    const height = document.querySelector('.track')?.getAttribute('style');
    expect(screen.getByLabelText('Zoom')).toHaveValue('100');
    await fireEvent.change(screen.getByLabelText('Zoom'), { target: { value: '200' } });
    expect(document.querySelector('.timeline-grid')).toHaveStyle({ width: 'calc(200% - 248px)' });
    expect(document.querySelector('.track')?.getAttribute('style')).toBe(height);
    expect(fetchTimeline).toHaveBeenCalledTimes(3);
    await fireEvent.click(screen.getByRole('button', { name: 'Load next 3 timelines' }));
    await waitFor(() => expect(fetchTimeline).toHaveBeenCalledTimes(5));
  });
});
