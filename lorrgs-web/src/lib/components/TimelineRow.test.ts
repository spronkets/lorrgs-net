import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';
import TimelineRow from './TimelineRow.svelte';
import { rows, response } from '../testing/timelineFixtures';
import { TIMELINE_ZOOM_LEVELS } from '../timelineUtils';

function props() {
  return {
    report: rows[0].report,
    rank: 1,
    edition: 'anniversary',
    axisDuration: 300000,
    ticks: [0, 30000],
    hidden: new Set<number>(),
    castSpellIds: new Set([31884]),
    zoom: 100,
    showIcons: true,
    showCasts: true,
    showBuffs: true,
    showDeaths: true,
    separateSpells: false,
    loading: false,
    batchLoading: false,
    onload: vi.fn(),
    oncopy: vi.fn()
  };
}

describe('TimelineRow', () => {
  it('labels inferred markers without presenting them as observed player casts', async () => {
    const input = props();
    render(TimelineRow, {
      ...input,
      timeline: {
        ...response(input.report),
        spells: [{ id: 267134, name: 'Drop Void Meta', icon: null }],
        events: [
          { spellId: 267134, timestamp: 10000, duration: 0, kind: 'marker', isInferred: true }
        ]
      }
    });
    const marker = screen.getByRole('button', { name: /Drop Void Meta.*inferred/ });
    expect(marker).toHaveClass('cast');
    await fireEvent.focus(marker);
    expect(marker).toHaveClass('spell-highlighted');
  });
  it('keeps icons above the lower event bars and centers events within separate lanes', async () => {
    const input = props();
    const { container, rerender } = render(TimelineRow, {
      ...input,
      timeline: response(input.report)
    });
    const cast = container.querySelector<HTMLElement>('.event.cast')!;
    const buff = container.querySelector<HTMLElement>('.event.buff')!;
    expect(cast.style.top).toBe('19px');
    expect(buff.style.top).toBe('43px');
    await rerender({ ...input, timeline: response(input.report), separateSpells: true });
    expect(cast.style.top).toMatch(/px$/);
    expect(buff.style.top).toMatch(/px$/);
    expect((parseFloat(cast.style.top) - 15) % 26).toBe(0);
    expect((parseFloat(buff.style.top) - 15) % 26).toBe(0);
  });

  it('shows this fight’s phases even before event loading and updates with the row metadata', async () => {
    const input = props();
    const report = {
      ...input.report,
      fights: [{ ...input.report.fights![0], phases: [{ id: 2, timestamp: 45000 }] }]
    };
    const { rerender } = render(TimelineRow, { ...input, report });
    expect(screen.getByRole('img', { name: 'Phase 2 at 0:45' })).toHaveStyle({ left: '15%' });
    expect(screen.queryByRole('img', { name: 'Phase 2 at 1:00' })).not.toBeInTheDocument();
    await rerender({
      ...input,
      report: {
        ...report,
        fights: [{ ...report.fights[0], phases: [{ id: 2, timestamp: 90000 }] }]
      }
    });
    expect(screen.getByRole('img', { name: 'Phase 2 at 1:30' })).toHaveStyle({ left: '30%' });
    expect(screen.queryByRole('img', { name: 'Phase 2 at 0:45' })).not.toBeInTheDocument();
  });

  it('disables magnification at maximum zoom while preserving spell inspection', async () => {
    const input = props();
    const { container, rerender } = render(TimelineRow, {
      ...input,
      timeline: response(input.report),
      zoom: TIMELINE_ZOOM_LEVELS[TIMELINE_ZOOM_LEVELS.length - 1]
    });
    const spell = screen.getByRole('button', { name: /0:10 · Avenging Wrath · DPS/ });
    await fireEvent.mouseEnter(spell);
    expect(container.querySelector('.magnified-section')).toBeNull();
    expect(spell).toHaveClass('spell-highlighted');
    await fireEvent.mouseLeave(spell);
    await rerender({
      ...input,
      timeline: response(input.report),
      zoom: TIMELINE_ZOOM_LEVELS[TIMELINE_ZOOM_LEVELS.length - 2]
    });
    await fireEvent.mouseEnter(spell);
    expect(container.querySelector('.magnified-section')).not.toBeNull();
  });

  it('shows loading feedback in the centered row state without a load button', () => {
    const { container } = render(TimelineRow, { ...props(), loading: true, batchLoading: true });
    const message = screen.getByRole('status');
    expect(message).toHaveTextContent('Loading report events…');
    expect(message).toHaveClass('loading-message');
    expect(message.parentElement).toBe(container.querySelector('.track'));
    expect(screen.queryByRole('button', { name: /Load report/ })).toBeNull();
  });

  it('shows known metadata and an accessible report link without event data and delegates loading', async () => {
    const input = props();
    const { container } = render(TimelineRow, input);
    expect(container.querySelector('.fight-end')?.getAttribute('style')).toContain('80%');
    expect(container.querySelector('.fight-end-label')?.textContent?.trim()).toBe('4:00');
    expect(screen.getByText('100 (Top 1%)')).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Player0' }).getAttribute('href')).toBe(
      'https://fresh.warcraftlogs.com/reports/Report0#fight=1'
    );
    expect(
      screen.getByRole('link', { name: 'Open report analyzer' }).getAttribute('href')
    ).toContain('playerName=Player0');
    await fireEvent.click(screen.getByRole('button', { name: 'Load report Report 0' }));
    expect(input.onload).toHaveBeenCalledWith(input.report);
  });

  it('delegates copy beside the end line and measures highlighted casts without changing row height', async () => {
    const input = props();
    const timeline = {
      ...response(input.report),
      events: [
        ...response(input.report).events,
        {
          spellId: 31884,
          timestamp: 130000,
          duration: null,
          kind: 'cast' as const
        }
      ]
    };
    const { container } = render(TimelineRow, { ...input, timeline });
    const track = container.querySelector('.track')!;
    const height = track.getAttribute('style');
    const spell = screen.getByRole('button', { name: /0:10 · Avenging Wrath · DPS/ });
    await fireEvent.mouseEnter(spell);
    expect(track.querySelector('.magnified-section')).not.toBeNull();
    expect(spell).toHaveClass('spell-highlighted');
    expect(track.querySelector('.cast-interval span')?.textContent).toBe('120s');
    expect(track.getAttribute('style')).toBe(height);
    await fireEvent.mouseLeave(spell);
    expect(track.querySelector('.magnified-section')).toBeNull();
    expect(track.querySelector('.cast-interval')).toBeNull();
    const copy = screen.getByRole('button', { name: 'Copy note' });
    expect(copy.parentElement).toBe(track);
    expect(copy.getAttribute('style')).toContain('80% + 4px');
    await fireEvent.click(copy);
    expect(input.oncopy).toHaveBeenCalledWith(timeline);
  });

  it('renders empty, hidden, incomplete and retry states distinctly', async () => {
    const input = props();
    const timeline = response(input.report);
    const { rerender } = render(TimelineRow, { ...input, timeline: { ...timeline, events: [] } });
    expect(
      screen.getByText(
        'Warcraft Logs returned no cast, buff or death events for this player and fight.'
      )
    ).toBeTruthy();
    await rerender({ ...input, timeline, hidden: new Set([31884]) });
    expect(screen.getByText('No events match the spell / timeline selection.')).toBeTruthy();
    await rerender({ ...input, timeline: { ...timeline, isComplete: false } });
    expect(screen.getByText('Partial timeline — event limit reached')).toBeTruthy();
    await rerender({
      ...input,
      timeline: undefined,
      error: 'API Error: 429 Too Many Requests - {"traceId":"private"}'
    });
    expect(
      screen.getByText(
        "Warcraft Logs needs a breather. We've hit its request limit, so please try again later."
      )
    ).toBeInTheDocument();
    expect(screen.queryByText(/traceId/)).not.toBeInTheDocument();
    await fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
    expect(input.onload).toHaveBeenCalledWith(input.report);
  });
});
