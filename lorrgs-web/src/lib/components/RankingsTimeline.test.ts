import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { getBossTimeline, getReportTimeline } from '../api';
import type { ReportTimeline } from '../models/ReportTimeline';
import { bossResponse, rows, response } from '../testing/timelineFixtures';
import RankingsTimeline from './RankingsTimeline.svelte';
import { TIMELINE_ZOOM_LEVELS } from '../timelineUtils';

vi.mock('../api', () => ({ getReportTimeline: vi.fn(), getBossTimeline: vi.fn() }));
const fetchTimeline = vi.mocked(getReportTimeline);
beforeEach(() => {
  fetchTimeline.mockReset();
  vi.mocked(getBossTimeline).mockReset();
  vi.mocked(getBossTimeline).mockImplementation(async (report) => bossResponse(report));
  fetchTimeline.mockImplementation(async (report) => response(report));
});
afterEach(() => vi.restoreAllMocks());

describe('ranking cooldown comparison', () => {
  it('shows opt-in self-use mana events above the original timeline without adding rows', async () => {
    const report = {
      ...rows[0].report,
      timeline: {
        ...response(rows[0].report),
        maxHealth: 12000,
        maxMana: 5500,
        spells: [
          { id: 1, name: 'Holy Light', icon: '' },
          { id: 2, name: 'Mana Potion', icon: '' }
        ],
        events: [
          { spellId: 1, timestamp: 1000, duration: null, kind: 'cast' as const },
          { spellId: 1, timestamp: 1000, duration: null, kind: 'mana-spent' as const, amount: 250 },
          {
            spellId: 2,
            timestamp: 2000,
            duration: null,
            kind: 'mana-returned' as const,
            amount: 500
          }
        ]
      }
    };
    const { container } = render(RankingsTimeline, {
      reports: [{ report, rank: 1 }],
      edition: 'anniversary'
    });
    expect(container.querySelector('.survival-row')).toBeNull();
    expect(container.querySelector('.timeline-controls fieldset'))
      .toContainElement(screen.getByRole('checkbox', { name: 'Show healing received' }));
    expect(container.querySelector('.timeline-controls fieldset'))
      .toContainElement(screen.getByRole('checkbox', { name: 'Show mana received' }));
    expect(screen.getByRole('checkbox', { name: 'Show healing received' }).closest('label'))
      .toHaveClass('option-toggle');
    expect(screen.getByRole('checkbox', { name: 'Show mana received' }).closest('label'))
      .toHaveClass('option-toggle');
    expect(screen.queryByRole('checkbox', { name: 'Show mana spent' })).not.toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: 'Show mana received' })).not.toBeChecked();
    expect(container.querySelector('.mana-event')).toBeNull();
    await fireEvent.click(screen.getByRole('checkbox', { name: 'Show mana received' }));
    expect(container.querySelectorAll('.mana-event')).toHaveLength(1);
    expect(container.querySelector('.mana-event[aria-label*="Holy Light"]')).toBeNull();
    await waitFor(() =>
      expect(container.querySelectorAll('.player-row .mana-event:not(.below)')).toHaveLength(1)
    );
    expect(container.querySelector('.player-row .mana-event[aria-label*="Holy Light"]')).toBeNull();
    expect(container.querySelector('.player-row .mana-event[aria-label*="Mana Potion"]')).not.toBeNull();
    await fireEvent.click(screen.getByRole('checkbox', { name: 'Show healing received' }));
    expect(container.querySelectorAll('.player-row')).toHaveLength(1);
    expect(container.querySelector('.survival-row')).toBeNull();
    await fireEvent.click(screen.getByRole('checkbox', { name: 'Show healing received' }));
    await fireEvent.click(screen.getByRole('checkbox', { name: 'Show mana received' }));
    expect(container.querySelectorAll('.mana-event')).toHaveLength(0);
    expect(container.querySelector('.mana-event[aria-label*="Mana Potion"]')).toBeNull();
    await waitFor(() => expect(container.querySelector('.mana-event')).toBeNull());
    expect(fetchTimeline).not.toHaveBeenCalled();
  });
  it('keeps defensives and health consumables above without duplicates', async () => {
    const names = ['Shield Wall', 'Pain Suppression', 'Super Healing Potion', 'Haste Potion'];
    const report = {
      ...rows[0].report,
      timeline: {
        ...response(rows[0].report),
        spells: names.map((name, index) => ({ id: index + 1, name, icon: '' })),
        events: names.map((_, index) => ({
          spellId: index + 1,
          timestamp: 1000 * (index + 1),
          duration: null,
          kind: index === 1 ? ('buff' as const) : ('cast' as const)
        }))
      }
    };
    const { container } = render(RankingsTimeline, {
      reports: [{ report, rank: 1 }],
      edition: 'anniversary'
    });
    expect(container.querySelector('.survival-row')).toBeNull();
    const survival = container.querySelector('.player-row')!;
    expect(survival.querySelector('[aria-label*="Shield Wall"]')).not.toBeNull();
    expect(survival.querySelector('[aria-label*="Pain Suppression"]')).not.toBeNull();
    expect(survival).toHaveTextContent('Super Healing Potion');
    expect(survival.querySelectorAll('.event.below')).toHaveLength(0);
    expect(survival.querySelectorAll('.event:not(.below)')).toHaveLength(4);
    expect(survival.querySelector('.event[aria-label*="Super Healing Potion"]'))
      .toHaveStyle({ '--spell-color': '#69d98b' });
    await fireEvent.click(screen.getByRole('checkbox', { name: 'Show healing received' }));
    expect(survival.querySelectorAll('.event.below')).toHaveLength(0);
    expect(container.querySelectorAll('.player-row')).toHaveLength(1);
    expect(fetchTimeline).not.toHaveBeenCalled();
  });
  it('toggles direct heals and HoT ticks independently of damage taken', async () => {
    const report = {
      ...rows[0].report,
      timeline: {
        ...response(rows[0].report),
        spells: [
          { id: 1, name: 'Direct healing', icon: '' },
          { id: 2, name: 'Periodic healing', icon: '' },
          { id: 3, name: 'Incoming damage', icon: '' },
          { id: 4, name: 'Mana Burn', icon: '' }
        ],
        events: [
          { spellId: 1, timestamp: 1000, duration: null, kind: 'heal' as const },
          { spellId: 2, timestamp: 2000, duration: null, kind: 'hot' as const },
          { spellId: 3, timestamp: 3000, duration: null, kind: 'damage' as const },
          { spellId: 4, timestamp: 3000, duration: null, kind: 'mana-spent' as const,
            amount: 125, isManaDrain: true, sourceName: 'Enemy' }
        ]
      }
    };
    const { container } = render(RankingsTimeline, {
      reports: [{ report, rank: 1 }],
      edition: 'anniversary'
    });
    expect(container.querySelectorAll('.player-row .healing')).toHaveLength(0);
    expect(screen.queryByRole('button', { name: 'Direct healing' })).not.toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: 'Show damage taken' })).not.toBeChecked();
    await fireEvent.click(screen.getByRole('checkbox', { name: 'Show healing received' }));
    await waitFor(() => expect(container.querySelectorAll('.player-row .healing.below')).toHaveLength(2));
    expect(container.querySelector('.damage-taken')).toBeNull();
    await fireEvent.click(screen.getByRole('checkbox', { name: 'Show damage taken' }));
    expect(container.querySelector('.mana-event.below')).toHaveStyle({ '--spell-color': '#d9ad69' });
    expect(screen.getByRole('button', { name: 'Mana Burn' })).toBeInTheDocument();
    await waitFor(() =>
      expect(container.querySelectorAll('.player-row .damage-taken')).toHaveLength(1)
    );
    expect(screen.getByRole('button', { name: 'Direct healing' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Periodic healing' })).toBeInTheDocument();
    await fireEvent.click(screen.getByRole('checkbox', { name: 'Show healing received' }));
    await waitFor(() => expect(container.querySelectorAll('.player-row .healing')).toHaveLength(0));
    expect(container.querySelectorAll('.damage-taken.below')).toHaveLength(1);
    await fireEvent.click(screen.getByRole('checkbox', { name: 'Show damage taken' }));
    expect(container.querySelector('.damage-taken')).toBeNull();
    expect(container.querySelector('.mana-event')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Mana Burn' })).not.toBeInTheDocument();
  });
  it('removes spells exclusive to filtered players and restores them without refetching', async () => {
    const reports = rows.slice(0, 2).map((row, index) => ({
      ...row,
      report: {
        ...row.report,
        timeline: {
          ...response(row.report),
          spells: [{ id: 100 + index, name: index ? 'Healer ability' : 'Tank ability', icon: '' }],
          events: [{ spellId: 100 + index, timestamp: 1000, duration: null, kind: 'cast' as const }]
        }
      }
    }));
    const { rerender } = render(RankingsTimeline, {
      reports,
      edition: 'anniversary',
      compositionMode: true
    });
    await screen.findByRole('button', { name: 'Tank ability' });
    expect(screen.getByRole('button', { name: 'Healer ability' })).toBeInTheDocument();
    await rerender({ reports: reports.slice(1), edition: 'anniversary', compositionMode: true });
    await waitFor(() =>
      expect(screen.queryByRole('button', { name: 'Tank ability' })).not.toBeInTheDocument()
    );
    expect(screen.getByRole('button', { name: 'Healer ability' })).toBeInTheDocument();
    await rerender({ reports, edition: 'anniversary', compositionMode: true });
    await screen.findByRole('button', { name: 'Tank ability' });
    expect(fetchTimeline).not.toHaveBeenCalled();
  });
  it('gives spec boss and player timelines separate matching axis headers', () => {
    const { container } = render(RankingsTimeline, { reports: rows, edition: 'anniversary' });
    const boss = screen.getByRole('region', { name: 'Boss timelines' });
    const players = screen.getByRole('region', { name: 'Player timelines' });
    expect(boss.querySelector('.axis-label')).toHaveTextContent('Boss');
    expect(players.querySelector('.axis-label')).toHaveTextContent('Player');
    expect(boss.querySelector('.boss-row')).not.toBeNull();
    expect(boss.querySelector('.player-row')).toBeNull();
    expect(players.querySelectorAll('.player-row')).toHaveLength(rows.length);
    expect(boss.querySelector('.axis')?.textContent).toBe(
      players.querySelector('.axis')?.textContent
    );
    expect(container.querySelectorAll('.position-label')).toHaveLength(2);
  });
  it('updates the matching player phases when corrected boss data arrives after player data', async () => {
    let resolveBoss!: (_timeline: ReturnType<typeof bossResponse>) => void;
    vi.mocked(getBossTimeline).mockImplementation(
      () =>
        new Promise((resolve) => {
          resolveBoss = resolve;
        })
    );
    const report = {
      ...rows[0].report,
      timeline: response(rows[0].report),
      fights: [{ ...rows[0].report.fights[0], phases: [{ id: 2, timestamp: 20000 }] }]
    };
    const { container } = render(RankingsTimeline, {
      reports: [{ report, rank: 1 }],
      edition: 'retail'
    });
    expect(container.querySelector('.player-row [aria-label="Phase 2 at 0:20"]')).not.toBeNull();
    resolveBoss({
      ...bossResponse(report),
      phases: [
        { id: 1.5, timestamp: 20000, isIntermission: true },
        { id: 2, timestamp: 90000, notePhaseId: 3 }
      ]
    });
    await waitFor(() => {
      expect(
        container.querySelector('.player-row [aria-label="Intermission 1.5 at 0:20"]')
      ).not.toBeNull();
      expect(container.querySelector('.player-row [aria-label="Phase 2 at 1:30"]')).not.toBeNull();
      expect(container.querySelector('.player-row [aria-label="Phase 2 at 0:20"]')).toBeNull();
    });
    expect(fetchTimeline).not.toHaveBeenCalled();
  });
  it('selects a target before Analyze and reuses bundled reference data across sorting', async () => {
    const reports = rows.map(({ report, rank }) => ({
      rank,
      report: { ...report, timeline: response(report), bossTimeline: bossResponse(report) }
    }));
    const { container, rerender } = render(RankingsTimeline, { reports, edition: 'anniversary' });
    await screen.findByRole('button', { name: '0:30 · Boss Strike' });
    const targets = screen.getAllByRole('button', { name: 'Use as boss reference' });
    expect(targets[0]).toHaveAttribute('aria-pressed', 'true');
    expect(targets[0]).toBeDisabled();
    expect(targets[0]).toHaveAttribute('title', 'Current boss reference');
    expect(screen.queryByText(/^Reference:/)).not.toBeInTheDocument();
    expect(targets[1].nextElementSibling).toHaveAttribute('aria-label', 'Open report analyzer');
    await fireEvent.click(targets[1]);
    await waitFor(() =>
      expect(container.querySelector('.boss-row a')).toHaveAttribute(
        'href',
        expect.stringContaining('Report1')
      )
    );
    expect(targets[1]).toHaveAttribute('aria-pressed', 'true');
    expect(targets[0]).toHaveAttribute('aria-pressed', 'false');
    expect(targets[0]).toBeEnabled();
    expect(targets[1]).toBeDisabled();
    await rerender({ reports: [...reports].reverse(), edition: 'anniversary' });
    expect(container.querySelector('.boss-row a')).toHaveAttribute(
      'href',
      expect.stringContaining('Report1')
    );
    expect(container.querySelector('.player-row .rank')?.textContent).toBe('5');
    await rerender({ reports: reports.slice(2), edition: 'anniversary' });
    await waitFor(() =>
      expect(container.querySelector('.boss-row a')).toHaveAttribute(
        'href',
        expect.stringContaining('Report2')
      )
    );
    expect(getBossTimeline).not.toHaveBeenCalled();
    expect(fetchTimeline).not.toHaveBeenCalled();
  });

  it('places the legend directly above the table outside its scrollable grid', () => {
    const { container } = render(RankingsTimeline);
    const legend = screen.getByText('Power windows').closest('.spell-inspector');
    expect(container.querySelector('.timeline-panel')?.firstElementChild).toBe(legend);
    expect(legend?.nextElementSibling).toBe(container.querySelector('.timeline-scroll'));
    expect(container.querySelector('.timeline-scroll')?.contains(legend)).toBe(false);
  });

  it('uses the first filtered/sorted report for the boss reference without changing player ranks', async () => {
    const reports = rows.map(({ report, rank }) => ({
      rank,
      report: { ...report, timeline: response(report), bossTimeline: bossResponse(report) }
    }));
    const { container, rerender } = render(RankingsTimeline, { reports, edition: 'anniversary' });
    await screen.findByRole('button', { name: '0:30 · Boss Strike' });
    expect(container.querySelector('.boss-row')?.parentElement?.nextElementSibling).toHaveAttribute(
      'aria-label',
      'Player timelines'
    );
    expect(container.querySelectorAll('.boss-row [aria-label="Phase 2 at 1:00"]')).toHaveLength(1);
    expect(container.querySelectorAll('.player-row [aria-label="Phase 2 at 1:00"]')).toHaveLength(
      5
    );
    await rerender({ reports: [...reports].reverse(), edition: 'anniversary' });
    await waitFor(() =>
      expect(container.querySelector('.boss-row a')).toHaveAttribute(
        'href',
        expect.stringContaining('Report4')
      )
    );
    expect(container.querySelector('.player-row .rank')?.textContent).toBe('5');
    expect(getBossTimeline).not.toHaveBeenCalled();
    expect(fetchTimeline).not.toHaveBeenCalled();
  });

  it('closes the magnifier at maximum zoom and restores it below the maximum', async () => {
    const maximum = TIMELINE_ZOOM_LEVELS.at(-1);
    const previous = TIMELINE_ZOOM_LEVELS[TIMELINE_ZOOM_LEVELS.length - 2];
    render(RankingsTimeline, { reports: rows.slice(0, 1), edition: 'anniversary' });
    expect(
      Array.from(screen.getByLabelText<HTMLSelectElement>('Zoom').options, (option) =>
        Number(option.value)
      )
    ).toEqual([...TIMELINE_ZOOM_LEVELS]);
    const spell = await screen.findByRole('button', {
      name: /0:10 · Avenging Wrath · Cast · DPS/
    });
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
    expect(screen.getByRole('tooltip')).toHaveTextContent(`${maximum}%`);
    expect(fetchTimeline).toHaveBeenCalledTimes(1);
  });

  it('keeps spells combined with magnification and no separate-spells control', async () => {
    const { container } = render(RankingsTimeline, {
      reports: rows.slice(0, 1),
      edition: 'anniversary'
    });
    const spell = await screen.findByRole('button', {
      name: /0:10 · Avenging Wrath · Cast · DPS/
    });
    expect(screen.queryByRole('checkbox', { name: 'Separate spells' })).not.toBeInTheDocument();
    await fireEvent.mouseEnter(spell);
    expect(screen.getByRole('tooltip')).toBeInTheDocument();
    expect(container.querySelector('.magnified-section')).not.toBeNull();
    expect(spell).toHaveClass('spell-highlighted');
    await fireEvent.mouseLeave(spell);
    await fireEvent.mouseEnter(spell);
    expect(screen.getByRole('tooltip')).toBeInTheDocument();
    expect(container.querySelector('.magnified-section')).not.toBeNull();
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
    expect(screen.queryByText(/timelines ready|All timelines loaded/)).not.toBeInTheDocument();
    expect(screen.queryByText('Loading report events…')).not.toBeInTheDocument();
    expect(fetchTimeline).not.toHaveBeenCalled();
  });

  it('uses fixed-size SVG toolbar icons and preserves their accessible toggles', async () => {
    render(RankingsTimeline, { reports: rows.slice(0, 1), edition: 'anniversary' });
    await screen.findByRole('button', { name: 'Copy note' });
    for (const name of ['Spell icons', 'Casts', 'Buff durations', 'Deaths', 'Show received buffs', 'Show healing received', 'Show damage taken', 'Show mana received']) {
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
    expect(screen.getByRole('dialog', { name: 'Copy Note' })).toBeVisible();
    expect(writeText).not.toHaveBeenCalled();
    await fireEvent.click(screen.getByRole('button', { name: 'Copy to clipboard' }));
    await waitFor(() =>
      expect(
        screen.getByText('Copied selected casts for Player0.').closest('[role="status"]')
      ).not.toBeNull()
    );
    expect(container.querySelector('p[role="status"]')).toBeNull();
    expect(writeText).toHaveBeenCalledTimes(1);
    expect(fetchTimeline).toHaveBeenCalledTimes(1);
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
    expect(container.querySelector('.timeline-grid')).toHaveStyle({ '--timeline-zoom': '100' });
    expect(container.querySelector('.axis')).not.toHaveTextContent('5:30');
    await rerender({ reports: reports.slice(0, 1), edition: 'anniversary' });
    expect(container.querySelector('.timeline-grid')).toHaveStyle({ '--timeline-zoom': '100' });
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
    expect(screen.queryByText(/timelines ready|All timelines loaded/)).not.toBeInTheDocument();
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
      screen.getByText(/Player0 · 0:20 · Divine Shield · Buff · 0:08 measured uptime/)
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
    const marker = screen.getByRole('button', {
      name: /0:30 · Bloodlust · Buff · 0:40 measured uptime · DPS/
    });
    expect(marker).toHaveClass('buff', 'power-window');
    expect(marker).not.toHaveClass('below');
    await fireEvent.click(screen.getByRole('checkbox', { name: 'Show healing received' }));
    expect(marker).not.toHaveClass('below');
    await fireEvent.mouseEnter(marker);
    expect(screen.getByRole('tooltip').querySelector('.event-details')).toHaveTextContent('Bloodlust');
    await fireEvent.mouseLeave(marker);
    await fireEvent.click(screen.getByRole('checkbox', { name: 'Show received buffs' }));
    expect(screen.getByRole('button', { name: /0:30 · Bloodlust · Buff/ })).toHaveClass('below');
    await fireEvent.mouseEnter(screen.getByRole('button', { name: /0:30 · Bloodlust · Buff/ }));
    expect(screen.getByRole('tooltip').querySelector('.event-details')).toHaveTextContent('Bloodlust');
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
    expect(
      screen.queryByRole('button', { name: /0:10 · Avenging Wrath/ })
    ).not.toBeInTheDocument();
    const height = document.querySelector('.track')?.getAttribute('style');
    expect(screen.getByLabelText('Zoom')).toHaveValue('100');
    await fireEvent.change(screen.getByLabelText('Zoom'), { target: { value: '200' } });
    expect(document.querySelector('.timeline-grid')).toHaveStyle({ '--timeline-zoom': '200' });
    expect(document.querySelector('.track')?.getAttribute('style')).toBe(height);
    expect(fetchTimeline).toHaveBeenCalledTimes(3);
    await fireEvent.click(screen.getByRole('button', { name: 'Load next 3 timelines' }));
    await waitFor(() => expect(fetchTimeline).toHaveBeenCalledTimes(5));
  });
});
