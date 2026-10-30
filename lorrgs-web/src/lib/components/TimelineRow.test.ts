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
    loading: false,
    onload: vi.fn(),
    oncopy: vi.fn()
  };
}

describe('TimelineRow', () => {
  it('links the player name to their profile separately from the report', () => {
    const input = props();
    render(TimelineRow, {
      ...input,
      report: {
        ...input.report,
        players: [{ ...input.report.players[0], serverRegion: 'EU', serverName: 'Thunderstrike' }]
      }
    });
    expect(screen.getByRole('link', { name: 'Player0' })).toHaveAttribute(
      'href', 'https://fresh.warcraftlogs.com/character/eu/thunderstrike/player0'
    );
    expect(screen.getByRole('link', { name: 'Open report in Warcraft Logs' })).toHaveAttribute(
      'href', 'https://fresh.warcraftlogs.com/reports/Report0#fight=1'
    );
  });
  it('uses green on healing cast icons and their measured connectors', () => {
    const input = props();
    const timeline = response(input.report);
    timeline.spells = [{ id: 1, name: 'Chain Heal', icon: 'spell_nature_healingwavegreater.jpg' }];
    timeline.events = [{ spellId: 1, timestamp: 3000, castStart: 1000, duration: 2000, kind: 'cast' }];
    const { container } = render(TimelineRow, { ...input, timeline });
    for (const selector of ['.event.cast', '.cast-connectors', '.cast-window'])
      expect(container.querySelector(selector)).toHaveStyle({ '--spell-color': '#69d98b' });
    expect(container.querySelector('.event.cast img')).not.toBeNull();
    expect(container.querySelector('.event.cast')).not.toHaveAttribute('title');
    expect(container.querySelector('.event.cast')).toHaveAttribute('aria-label',
      expect.stringContaining('Chain Heal'));
    expect(container.querySelector('.fight-end')).not.toHaveAttribute('title');
  });
  it('collapses unused lower-lane space as incoming effects are toggled', async () => {
    const input = props();
    const timeline = response(input.report);
    timeline.events = [
      { spellId: 1, timestamp: 1000, duration: null, kind: 'cast' },
      { spellId: 2, timestamp: 2000, duration: null, kind: 'heal', sourceName: 'Healer' },
      { spellId: 3, timestamp: 3000, duration: null, kind: 'mana-returned', sourceName: 'Other' },
      { spellId: 4, timestamp: 4000, duration: null, kind: 'damage' }
    ];
    const { container, rerender } = render(TimelineRow, { ...input, timeline, mergedResources: true });
    expect(container.querySelector('.track')).toHaveStyle({ height: '68px' });
    for (const toggle of ['showDirectHeals', 'showManaReceived', 'showDamageTaken']) {
      await rerender({ ...input, timeline, mergedResources: true,
        showDirectHeals: false, showManaReceived: false, showDamageTaken: false, [toggle]: true });
      expect(container.querySelector('.track')).toHaveStyle({ height: '108px' });
    }
    await rerender({ ...input, timeline, mergedResources: true,
      showDirectHeals: false, showManaReceived: false, showDamageTaken: false });
    expect(container.querySelector('.track')).toHaveStyle({ height: '68px' });
  });
  it('mirrors resource icons below the shared baseline without duplicating consumable casts', () => {
    const input = props();
    const timeline = response(input.report);
    timeline.spells = [
      { id: 1, name: 'Shadow Bolt', icon: null },
      { id: 2, name: 'Healing Potion', icon: null },
      { id: 3, name: 'Renew', icon: null }
    ];
    timeline.events = [
      { spellId: 1, timestamp: 10000, duration: null, kind: 'cast' },
      { spellId: 2, timestamp: 20000, duration: null, kind: 'cast' },
      { spellId: 2, timestamp: 20000, duration: null, kind: 'heal', amount: 2000 },
      { spellId: 3, timestamp: 30000, duration: 10000, kind: 'buff' }
    ];
    const { container } = render(TimelineRow, {
      ...input, timeline, mergedResources: true, showDirectHeals: true, showReceivedBuffs: true
    });
    expect(container.querySelector('.track')).toHaveStyle({ height: '108px' });
    expect(container.querySelectorAll('.event')).toHaveLength(3);
    expect(container.querySelector('.event:not(.below)')).toHaveStyle({ top: '19px' });
    expect(container.querySelectorAll('.event.below')).toHaveLength(1);
    expect(container.querySelector('.event.healing:not(.below)'))
      .toHaveStyle({ top: '19px', '--spell-color': '#69d98b' });
    for (const icon of container.querySelectorAll('.event.below'))
      expect(icon).toHaveStyle({ top: '75px', '--spell-color': '#69d98b' });
    for (const line of container.querySelectorAll('.cast-connectors.below'))
      expect(line).toHaveStyle({ top: '47px', width: '0%' });
    expect(container.querySelector('.cast-window[aria-label*="Renew"]')).not.toBeNull();
  });
  it.each(['heal', 'hot', 'damage', 'mana-spent', 'mana-returned'] as const)(
    'anchors %s icons left of the hit timestamp with a landing line', (kind) => {
      const input = props();
      const timeline = response(input.report);
      timeline.events = [{ spellId: 31884, timestamp: 30000, duration: null, kind }];
      const { container } = render(TimelineRow, { ...input, timeline, survivalMode: true,
        showDirectHeals: true, showHots: true, showDamageTaken: true, showManaSpent: true, showManaReceived: true });
      expect(container.querySelector('.event')).toHaveClass('cast-icon');
      expect(container.querySelector('.cast-connectors')).toHaveClass('instant');
      expect(container.querySelector('.cast-connectors')).toHaveStyle({ left: '10%', width: '0%' });
    }
  );
  it('labels estimated health explicitly and prefers recorded maximums', async () => {
    const input = props();
    const timeline = { ...response(input.report), maxHealth: null, estimatedMaxHealth: 10000 };
    const { container, rerender } = render(TimelineRow, {
      ...input, timeline, survivalMode: true, resourceMode: 'health'
    });
    expect(container.querySelector('.identity .player-heading')).toHaveTextContent(/^Health$/);
    expect(container.querySelector('.identity .health-capacity')).toHaveTextContent(/^~10,000$/);
    expect(container.querySelector('.track .resource-capacity')).toBeNull();
    await rerender({
      ...input, timeline: { ...timeline, maxHealth: 12000 },
      survivalMode: true, resourceMode: 'health'
    });
    expect(container.querySelector('.identity .health-capacity')).toHaveTextContent(/^12,000$/);
    await rerender({
      ...input, timeline: { ...timeline, maxMana: 7173 },
      survivalMode: true, resourceMode: 'mana'
    });
    expect(container.querySelector('.resource-capacity')).toHaveTextContent('Max mana: 7,173');
  });
  it('shows local estimate ranges and collapses equal estimates to one value', async () => {
    const input = props();
    const timeline = {
      ...response(input.report), maxHealth: null, estimatedMaxHealth: 15000,
      healthCapacities: [
        { timestamp: 0, maxHealth: null, estimatedMaxHealth: null },
        { timestamp: 1000, maxHealth: null, estimatedMaxHealth: 12000 },
        { timestamp: 2000, maxHealth: null, estimatedMaxHealth: 18000 },
        { timestamp: 3000, maxHealth: null, estimatedMaxHealth: 14000 }
      ]
    };
    const { container, rerender } = render(TimelineRow, {
      ...input, timeline, survivalMode: true, resourceMode: 'health'
    });
    expect(container.querySelector('.health-capacity')).toHaveTextContent(/^~12,000 - ~18,000$/);
    await rerender({
      ...input, survivalMode: true, resourceMode: 'health',
      timeline: { ...timeline, healthCapacities: [
        { timestamp: 1000, maxHealth: null, estimatedMaxHealth: 12000 },
        { timestamp: 2000, maxHealth: null, estimatedMaxHealth: 12000 }
      ] }
    });
    expect(container.querySelector('.health-capacity')).toHaveTextContent(/^~12,000$/);
    await rerender({
      ...input, survivalMode: true, resourceMode: 'health',
      timeline: { ...timeline, healthCapacities: [], estimatedMaxHealth: null }
    });
    expect(container.querySelector('.health-capacity')).toHaveTextContent(/^Unavailable$/);
  });
  it('anchors icons to cast completion and connects measured starts and ends, with one line for instant casts', () => {
    const input = props();
    const timeline = response(input.report);
    timeline.events = [
      { spellId: 31884, timestamp: 30000, castStart: 15000, duration: 15000, kind: 'cast' },
      { spellId: 31884, timestamp: 60000, duration: null, kind: 'cast' }
    ];
    const { container } = render(TimelineRow, { ...input, timeline });
    const icons = container.querySelectorAll<HTMLElement>('.cast-icon');
    expect(icons[0]).toHaveStyle({ left: '10%' });
    const connectors = container.querySelectorAll('.cast-connectors');
    expect(connectors[0]).toHaveStyle({ left: '5%', width: '5%', top: '19px' });
    expect(connectors[0]).not.toHaveClass('instant');
    expect(connectors[1]).toHaveClass('instant');
    expect(connectors[1]).toHaveStyle({ left: '20%', width: '0%' });
  });

  it('tracks the pointer across a duration instead of snapping to its start', async () => {
    const input = props();
    const timeline = {
      ...response(input.report),
      events: [{ spellId: 31884, timestamp: 10000, duration: 100000, kind: 'buff' as const }]
    };
    const { container } = render(TimelineRow, {
      ...input,
      timeline,
      survivalMode: true,
      resourceMode: 'health'
    });
    const track = container.querySelector<HTMLElement>('.track')!;
    vi.spyOn(track, 'getBoundingClientRect').mockReturnValue({
      left: 100,
      width: 1000,
      top: 0,
      bottom: 68,
      right: 1100,
      height: 68,
      x: 100,
      y: 0,
      toJSON: () => ({})
    });
    const buff = container.querySelector<HTMLElement>('.event.buff')!;
    await fireEvent.mouseEnter(buff, { clientX: 300, clientY: 40 });
    const section = container.querySelector<HTMLElement>('.magnified-section')!;
    // At 800% zoom the 342px preview spans 12.825s on this 300s axis.
    expect(parseFloat(section.style.left)).toBeCloseTo(17.8625);
    expect(parseFloat(section.style.width)).toBeCloseTo(4.275);
    await fireEvent.mouseMove(buff, { clientX: 400, clientY: 40 });
    expect(parseFloat(section.style.left)).toBeCloseTo(27.8625);
    await fireEvent.mouseLeave(buff);
    await fireEvent.mouseMove(track, { clientX: 500, clientY: 55 });
    const emptyAreaSection = container.querySelector<HTMLElement>('.magnified-section')!;
    expect(parseFloat(emptyAreaSection.style.left)).toBeCloseTo(37.8625);
    expect(emptyAreaSection.getAttribute('style')).not.toContain('undefined');
    expect(emptyAreaSection.style.getPropertyValue('--spell-color')).toBe('');
    await fireEvent.mouseOut(track, { relatedTarget: null });
    expect(container.querySelector('.magnified-section')).toBeNull();
  });

  it('shows the plain position in a separate column outside the player heading', () => {
    const { container } = render(TimelineRow, { ...props(), rank: 12 });
    const position = screen.getByLabelText('Position 12');
    expect(position).toHaveTextContent(/^12$/);
    expect(position.parentElement).toHaveClass('player-row');
    expect(position.nextElementSibling).toHaveClass('identity');
    expect(container.querySelector('.player-heading .rank')).toBeNull();
  });
  it.each([
    [1, 'Alliance'],
    [2, 'Horde']
  ] as const)('shows faction %i before guild and region', (factionId, faction) => {
    const input = props();
    render(TimelineRow, {
      ...input,
      report: {
        ...input.report,
        players: [
          { ...input.report.players![0], factionId, serverRegion: 'EU', serverName: 'Thunderstrike', guildName: 'Raid Guild' }
        ]
      }
    });
    const name = screen.getByText('Raid Guild');
    expect(name).toHaveClass('faction-name', faction.toLowerCase());
    expect(name).toHaveAttribute('title', faction);
    expect(name).toHaveTextContent('Raid Guild');
    expect(screen.getByText('Thunderstrike EU')).not.toHaveClass('faction-name', 'alliance', 'horde');
    expect(name.parentElement?.firstElementChild).toBe(name);
    expect(screen.queryByRole('img', { name: faction })).not.toBeInTheDocument();
  });

  it('does not invent a faction icon when faction is unknown', () => {
    const input = props();
    const { container } = render(TimelineRow, {
      ...input,
      report: { ...input.report, players: [{ ...input.report.players![0], factionId: null }] }
    });
    expect(container.querySelector('.faction-icon')).toBeNull();
  });

  it('handles incomplete player metadata without inventing a class, date, or analyzer link', async () => {
    const input = props();
    const { container, rerender } = render(TimelineRow, {
      ...input,
      report: {
        ...input.report,
        reportId: '',
        players: [],
        fights: [],
        startTime: 'invalid',
        percentile: null
      }
    });
    expect(screen.getByText('Unknown player').closest('a')).toBeNull();
    expect(screen.getByRole('link', { name: 'Open report in Warcraft Logs' })).toBeInTheDocument();
    expect(screen.getByText('0 (N/A)')).toBeInTheDocument();
    expect(container.querySelector('.player-spec-icon')).toBeNull();
    expect(container.querySelector('.report-time')).toBeNull();
    expect(screen.queryByRole('link', { name: 'Open report analyzer' })).not.toBeInTheDocument();
    await rerender({
      ...input,
      report: {
        ...input.report,
        players: [
          {
            ...input.report.players![0],
            specSlug: 'unknown',
            guildName: 'Raid Guild',
            serverRegion: 'EU'
          }
        ]
      }
    });
    expect(container.querySelector('.player-guild')).toHaveTextContent(/Raid Guild.*EU/);
    expect(container.querySelector('.player-spec-icon')).toBeNull();
  });

  it('delegates reference selection and disables the active reference', async () => {
    const input = props();
    const onreference = vi.fn();
    const { rerender } = render(TimelineRow, { ...input, onreference });
    const button = screen.getByRole('button', { name: 'Use as boss reference' });
    expect(button.parentElement).toHaveClass('player-heading');
    expect(button.parentElement?.lastElementChild).toBe(button);
    expect(button).toHaveAttribute('aria-pressed', 'false');
    await fireEvent.click(button);
    expect(onreference).toHaveBeenCalledWith(input.report);
    await rerender({ ...input, onreference, isReference: true });
    expect(button).toBeDisabled();
    expect(button).toHaveAttribute('aria-pressed', 'true');
  });
  it('shows role parse percentiles instead of row numbers in composition mode', async () => {
    const input = props();
    const { container, rerender } = render(TimelineRow, {
      ...input,
      compositionMode: true,
      report: {
        ...input.report,
        players: [{ ...input.report.players![0], performance: 2116, parsePercentile: 99 }]
      }
    });
    expect(screen.getByText('2,116 (Top 1%)')).toBeInTheDocument();
    expect(container.querySelector('.rank')).toBeNull();
    await rerender({ ...input, compositionMode: true });
    expect(screen.getByText('Parse unavailable')).toBeInTheDocument();
  });

  it('colors player names by class and updates when the player changes', async () => {
    const input = props();
    const report = {
      ...input.report,
      players: [{ ...input.report.players![0], name: 'Paladin', specSlug: 'holy-paladin' }]
    };
    const { container, rerender } = render(TimelineRow, { ...input, report });
    expect(screen.getByText('Paladin')).toHaveClass('class-name', 'paladin');
    const icon = container.querySelector('.player-spec-icon')!;
    expect(icon).toHaveAttribute('src', '/images/wow/specs/paladin-holy.jpg');
    expect(icon.nextElementSibling).toHaveTextContent('Paladin');
    await rerender({
      ...input,
      report: {
        ...report,
        players: [{ ...report.players[0], name: 'Mage', specSlug: 'fire-mage' }]
      }
    });
    expect(screen.getByText('Mage')).toHaveClass('class-name', 'mage');
    expect(container.querySelector('.player-spec-icon')).toHaveAttribute(
      'src',
      '/images/wow/specs/mage-fire.jpg'
    );
  });

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
  it('keeps icons above the lower event bars', () => {
    const input = props();
    const { container } = render(TimelineRow, {
      ...input,
      timeline: response(input.report)
    });
    const cast = container.querySelector<HTMLElement>('.event.cast')!;
    const buff = container.querySelector<HTMLElement>('.event.buff')!;
    expect(cast.style.top).toBe('19px');
    expect(buff.style.top).toBe('47px');
    expect(parseFloat(buff.style.top) - (parseFloat(cast.style.top) + 22)).toBe(6);
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
    const spell = screen.getByRole('button', { name: /0:10 · Avenging Wrath · Cast · DPS/ });
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
    const { container } = render(TimelineRow, { ...props(), loading: true });
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
    const reportLink = screen.getByRole('link', { name: 'Open report in Warcraft Logs' });
    expect(screen.getByText('Player0').closest('a')).toBeNull();
    expect(container.querySelector('.report-time')?.nextElementSibling).toBe(reportLink);
    expect(reportLink.querySelector('svg')).toHaveClass('lucide-external-link');
    expect(container.querySelector('.player-heading')).not.toHaveTextContent(input.report.title);
    expect(reportLink.getAttribute('href')).toBe(
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
    const spell = screen.getByRole('button', { name: /0:10 · Avenging Wrath · Cast · DPS/ });
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
    const retry = screen.getByRole('button', { name: 'Retry' });
    expect(retry.closest('.alert')).toHaveAttribute('data-variant', 'warning');
    expect(retry).toHaveAttribute('data-tone', 'warning');
    expect(retry).toHaveAttribute('data-outline', 'true');
    await fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
    expect(input.onload).toHaveBeenCalledWith(input.report);
  });
});
