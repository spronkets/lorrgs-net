import { render, screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';
import TimelineMagnifier from './TimelineMagnifier.svelte';
import { rows, response } from '../testing/timelineFixtures';

describe('TimelineMagnifier', () => {
  it('uses green on healing cast preview icons and their measured connectors', () => {
    const timeline = response(rows[0].report);
    timeline.spells = [{ id: 1, name: 'Chain Heal', icon: 'spell_nature_healingwavegreater.jpg' }];
    timeline.events = [
      { spellId: 1, timestamp: 12000, castStart: 10000, duration: 2000, kind: 'cast' }
    ];
    const { container } = render(TimelineMagnifier, {
      magnifier: {
        timeline,
        timestamp: 11000,
        start: 10000,
        span: 10000,
        x: 200,
        y: 400,
        zoom: 800
      },
      inspected: null,
      hidden: new Set<number>(),
      castSpellIds: new Set([1]),
      showCasts: true,
      showBuffs: true,
      showDeaths: true
    });
    for (const selector of ['.magnified-event.cast', '.cast-connectors', '.cast-window'])
      expect(container.querySelector(selector)).toHaveStyle({ '--spell-color': '#69d98b' });
  });
  it.each([
    ['mana-returned', '+1,250.13'],
    ['mana-spent', '-1,250.13']
  ] as const)('shows %s with its signed mana gain/loss color', (kind, amount) => {
    const timeline = response(rows[0].report);
    timeline.events = [{ spellId: 1, timestamp: 12000, duration: null, kind, amount: 1250.125 }];
    const { container } = render(TimelineMagnifier, {
      magnifier: {
        timeline,
        events: timeline.events,
        timestamp: 12000,
        start: 10000,
        span: 10000,
        x: 200,
        y: 400,
        zoom: 800
      },
      inspected: null,
      hidden: new Set<number>(),
      castSpellIds: new Set<number>(),
      showCasts: true,
      showBuffs: true,
      showDeaths: true
    });
    expect(container.querySelector('.detail-result .mana-cost')).toHaveTextContent(amount);
    expect(
      container.querySelector('.detail-result .mana-cost')?.classList.contains('mana-loss')
    ).toBe(kind === 'mana-spent');
    expect(container.querySelector('.detail-result')).not.toHaveTextContent(
      /Mana returned|Mana spent/
    );
  });
  it('shows recorded mana cost beside the cast name even when mana events are hidden', async () => {
    const timeline = response(rows[0].report);
    timeline.spells = [{ id: 1, name: 'Holy Light', icon: null }];
    timeline.events = [
      { spellId: 1, timestamp: 12000, castStart: 10000, duration: 2000, kind: 'cast' },
      {
        spellId: 1,
        timestamp: 12000,
        duration: null,
        kind: 'mana-spent',
        amount: 250.125,
        sourceName: timeline.playerName
      },
      { spellId: 1, timestamp: 13000, duration: null, kind: 'mana-spent', amount: 999 },
      {
        spellId: 1,
        timestamp: 12000,
        duration: null,
        kind: 'mana-spent',
        amount: 888,
        sourceName: 'Enemy'
      },
      {
        spellId: 1,
        timestamp: 12000,
        duration: null,
        kind: 'mana-spent',
        amount: 999,
        isManaDrain: true
      }
    ];
    const input = {
      magnifier: {
        timeline,
        timestamp: 11000,
        start: 10000,
        span: 10000,
        x: 200,
        y: 400,
        zoom: 800
      },
      inspected: null,
      hidden: new Set<number>(),
      castSpellIds: new Set([1]),
      showCasts: true,
      showBuffs: true,
      showDeaths: true
    };
    const { container, rerender } = render(TimelineMagnifier, input);
    expect(container.querySelector('.detail-name')).toHaveTextContent('Holy Light -250.13');
    expect(container.querySelector('.detail-name .mana-cost')).toHaveClass('mana-loss');
    expect(container.querySelectorAll('.event-details li')).toHaveLength(1);
    await rerender({
      ...input,
      magnifier: { ...input.magnifier, timestamp: 12000, hoveredEvent: timeline.events[0] }
    });
    expect(container.querySelector('.detail-name')).toHaveTextContent('Holy Light -250.13');
    await rerender({
      ...input,
      magnifier: {
        ...input.magnifier,
        timeline: {
          ...timeline,
          events: [
            ...timeline.events,
            { spellId: 1, timestamp: 12000, duration: null, kind: 'mana-spent', amount: 25 },
            { spellId: 2, timestamp: 12000, duration: null, kind: 'mana-spent', amount: 777 }
          ]
        }
      }
    });
    expect(container.querySelector('.detail-name')).toHaveTextContent('Holy Light -275.13');
    await rerender({
      ...input,
      magnifier: { ...input.magnifier, timeline: { ...timeline, events: [timeline.events[0]] } }
    });
    expect(container.querySelector('.mana-cost')).toBeNull();
  });
  it('labels recorded channel intervals without extending them past removal', async () => {
    const timeline = response(rows[0].report);
    timeline.spells = [{ id: 12051, name: 'Evocation', icon: null }];
    timeline.events = [{ spellId: 12051, timestamp: 10000, duration: 6000, kind: 'buff' }];
    const input = {
      magnifier: {
        timeline,
        timestamp: 12000,
        start: 10000,
        span: 10000,
        x: 200,
        y: 400,
        zoom: 800
      },
      inspected: null,
      hidden: new Set<number>(),
      castSpellIds: new Set<number>(),
      showCasts: true,
      showBuffs: true,
      showDeaths: true
    };
    const { container, rerender } = render(TimelineMagnifier, input);
    expect(container.querySelector('.detail-name')).toHaveTextContent(/^Evocation$/);
    expect(container.querySelector('.detail-time')).toHaveTextContent('0:10 - 0:16 (6s)');
    await rerender({ ...input, magnifier: { ...input.magnifier, timestamp: 16000 } });
    expect(container.querySelector('.detail-name')).toBeNull();
  });
  it.each([
    [-100, 16],
    [2000, 648]
  ])('clamps the complete zoom window inside the viewport at pointer x=%s', (x, left) => {
    const { container } = render(TimelineMagnifier, {
      magnifier: {
        timeline: response(rows[0].report),
        start: 0,
        span: 10000,
        x,
        y: -100,
        trackTop: -100,
        zoom: 800
      },
      inspected: null,
      hidden: new Set<number>(),
      castSpellIds: new Set<number>(),
      showCasts: true,
      showBuffs: true,
      showDeaths: true
    });
    expect(container.querySelector('.timeline-magnifier')).toHaveStyle({
      left: `${left}px`,
      top: '16px'
    });
  });
  it('mirrors the merged resource lane and buff duration bars in the zoom preview', () => {
    const timeline = response(rows[0].report);
    timeline.events = [
      { spellId: 1, timestamp: 12000, duration: null, kind: 'cast', displayBelow: false },
      {
        spellId: 2,
        timestamp: 12000,
        duration: null,
        kind: 'heal',
        displayBelow: true,
        amount: 100
      },
      { spellId: 3, timestamp: 12000, duration: 3000, kind: 'buff', displayBelow: true }
    ];
    const { container } = render(TimelineMagnifier, {
      magnifier: {
        timeline,
        events: timeline.events,
        mergedResources: true,
        timestamp: 12000,
        start: 10000,
        span: 10000,
        x: 200,
        y: 400,
        zoom: 800
      },
      inspected: null,
      hidden: new Set<number>(),
      castSpellIds: new Set([1]),
      showCasts: true,
      showBuffs: true,
      showDeaths: true
    });
    expect(container.querySelector('.magnified-track')).toHaveStyle({ height: '108px' });
    expect(container.querySelectorAll('.magnified-event.below')).toHaveLength(2);
    for (const icon of container.querySelectorAll('.magnified-event.below'))
      expect(icon).toHaveStyle({ top: '75px', '--spell-color': '#69d98b' });
    expect(container.querySelector('.cast-connectors.below')).toHaveStyle({ top: '47px' });
    expect(container.querySelector('.cast-window')).not.toBeNull();
  });
  it.each(['heal', 'hot', 'damage', 'mana-spent', 'mana-returned'] as const)(
    'aligns %s preview icons and hover detection to the left of the landing line',
    (kind) => {
      const timeline = response(rows[0].report);
      timeline.events = [{ spellId: 31884, timestamp: 12000, duration: null, kind }];
      const { container } = render(TimelineMagnifier, {
        magnifier: {
          timeline,
          events: timeline.events,
          resourceMode: 'health',
          timestamp: 11900,
          start: 10000,
          span: 10000,
          previewPixelsPerMillisecond: 0.1,
          x: 200,
          y: 400,
          zoom: 800
        },
        inspected: null,
        hidden: new Set<number>(),
        castSpellIds: new Set<number>(),
        showCasts: true,
        showBuffs: true,
        showDeaths: true
      });
      expect(container.querySelector('.magnified-event')).toHaveClass('cast-icon');
      expect(container.querySelector('.cast-connectors')).toHaveClass('instant');
      expect(container.querySelector('.cast-connectors')).toHaveStyle({ left: '20%', width: '0%' });
      expect(container.querySelectorAll('.event-details li')).toHaveLength(1);
    }
  );
  it('uses only cursor-time health evidence and invalidates it across aura changes', async () => {
    const timeline = {
      ...response(rows[0].report),
      maxHealth: 20000,
      estimatedMaxHealth: 19000,
      healthCapacities: [
        { timestamp: 10000, maxHealth: null, estimatedMaxHealth: 16127 },
        { timestamp: 13000, maxHealth: null, estimatedMaxHealth: null },
        { timestamp: 14000, maxHealth: 17000, estimatedMaxHealth: null },
        { timestamp: 15000, maxHealth: 20000, estimatedMaxHealth: null },
        { timestamp: 16000, maxHealth: 17000, estimatedMaxHealth: null }
      ]
    };
    const input = {
      magnifier: {
        timeline,
        resourceMode: 'health' as const,
        timestamp: 12000,
        start: 10000,
        span: 10000,
        x: 200,
        y: 400,
        zoom: 800
      },
      inspected: null,
      hidden: new Set<number>(),
      castSpellIds: new Set<number>(),
      showCasts: true,
      showBuffs: true,
      showDeaths: true
    };
    const { rerender } = render(TimelineMagnifier, input);
    expect(screen.getByText('Health ~16,127')).toBeInTheDocument();
    expect(screen.queryByText(/Max health:/)).not.toBeInTheDocument();
    await rerender({
      ...input,
      magnifier: { ...input.magnifier, timestamp: 14000 }
    });
    expect(screen.getByText('Health 17,000')).toBeInTheDocument();
    await rerender({
      ...input,
      magnifier: { ...input.magnifier, timestamp: 13500 }
    });
    expect(screen.getByText('Health')).toBeInTheDocument();
    await rerender({ ...input, magnifier: { ...input.magnifier, timestamp: 8999 } });
    expect(screen.getByText('Health')).toBeInTheDocument();
    await rerender({ ...input, magnifier: { ...input.magnifier, timestamp: 9900 } });
    expect(screen.getByText('Health ~16,127')).toBeInTheDocument();
    await rerender({ ...input, magnifier: { ...input.magnifier, timestamp: 15000 } });
    expect(screen.getByText('Health 20,000')).toBeInTheDocument();
    await rerender({ ...input, magnifier: { ...input.magnifier, timestamp: 16000 } });
    expect(screen.getByText('Health 17,000')).toBeInTheDocument();
  });
  it('shows each recorded target result throughout the cast hover interval', async () => {
    const timeline = response(rows[0].report);
    timeline.spells = [{ id: 1, name: 'Holy Shock', icon: null }];
    timeline.events = [
      {
        spellId: 1,
        timestamp: 12000,
        castStart: 10000,
        duration: 2000,
        kind: 'cast',
        castResults: [
          { targetId: 8, targetName: 'Ally', kind: 'heal', amount: 1250 },
          { targetId: 10, targetName: 'Other Ally', kind: 'heal', amount: 750.125 },
          { targetId: 11, targetName: 'Zero Heal', kind: 'heal', amount: 0 },
          { targetId: 12, targetName: 'Zero Damage', kind: 'damage', amount: 0 },
          { targetId: 13, targetName: 'Rounded Zero', kind: 'heal', amount: 0.001 },
          { targetId: 9, targetName: 'Enemy', kind: 'damage', amount: 2500 }
        ]
      }
    ];
    const input = {
      magnifier: {
        timeline,
        events: timeline.events,
        hoveredEvent: timeline.events[0],
        timestamp: 12000,
        start: 10000,
        span: 10000,
        x: 200,
        y: 400,
        zoom: 800
      },
      inspected: null,
      hidden: new Set<number>(),
      castSpellIds: new Set([1]),
      showCasts: true,
      showBuffs: true,
      showDeaths: true
    };
    const { container, rerender } = render(TimelineMagnifier, input);
    expect(container.querySelectorAll('.cast-results > .health-gain')).toHaveLength(2);
    expect(container.querySelectorAll('.cast-results > .health-gain')[0]).toHaveTextContent(
      '+1,250 to Ally'
    );
    expect(container.querySelectorAll('.cast-results > .health-gain')[1]).toHaveTextContent(
      '+750.13 to Other Ally'
    );
    expect(container.querySelector('.health-loss')).toHaveTextContent('-2,500 to Enemy');
    expect(container.querySelector('.cast-results')).not.toHaveTextContent(
      /Zero Heal|Zero Damage|Rounded Zero/
    );
    const row = container.querySelector('.detail-row');
    expect(row?.children).toHaveLength(3);
    expect(row?.querySelector('.detail-time')).toHaveTextContent('0:12');
    expect(row?.querySelector('.detail-name')).toHaveTextContent('Holy Shock');
    expect(row?.querySelector('.detail-result')).toHaveTextContent('+1,250 to Ally');
    expect(row?.querySelector('.detail-result')).toHaveTextContent('+750.13 to Other Ally');
    await rerender({ ...input, magnifier: { ...input.magnifier, timestamp: 11000 } });
    expect(container.querySelector('.event-details')).toHaveTextContent('Holy Shock');
    expect(container.querySelector('.health-gain')).toHaveTextContent('+1,250 to Ally');
    expect(container.querySelector('.health-loss')).toHaveTextContent('-2,500 to Enemy');
  });
  it('shows health before and after the hovered heal alongside its ability and healer', () => {
    const timeline = response(rows[0].report);
    timeline.spells = [{ id: 1, name: 'Holy Light', icon: null }];
    timeline.events = [
      {
        spellId: 1,
        timestamp: 12000,
        duration: null,
        kind: 'heal',
        sourceName: 'zzOLDHealing Stream Totem VI',
        amount: 3000,
        healthBeforePercent: 40,
        healthAfterPercent: 70,
        healthBeforeIsPreviousSnapshot: true
      }
    ];
    const { container } = render(TimelineMagnifier, {
      magnifier: {
        timeline,
        events: timeline.events,
        hoveredEvent: timeline.events[0],
        resourceMode: 'health',
        timestamp: 12000,
        start: 10000,
        span: 10000,
        x: 200,
        y: 400,
        zoom: 800
      },
      inspected: null,
      hidden: new Set<number>(),
      castSpellIds: new Set<number>(),
      showCasts: true,
      showBuffs: true,
      showDeaths: true
    });
    const details = container.querySelector('.event-details');
    expect(details).toHaveTextContent('Holy Light');
    expect(details?.querySelector('.health-source')).toHaveTextContent(
      '(zzOLDHealing Stream Totem VI)'
    );
    expect(details?.querySelector('.health-result')).toHaveTextContent('+3,000 (+30%)');
    expect(details?.querySelector('.health-result')).not.toHaveTextContent('70%');
    expect(details?.querySelectorAll('.health-gain')).toHaveLength(2);
    expect(details?.querySelector('.health-detail-line')).toHaveTextContent(
      'Holy Light (zzOLDHealing Stream Totem VI)'
    );
    expect(details?.querySelector('.health-gain')).toHaveTextContent('+3,000');
    expect(details).not.toHaveTextContent('Direct heal received');
  });
  it('lists instant casts when their left-anchored zoom icons overlap the cursor without a hovered event', () => {
    const timeline = response(rows[0].report);
    timeline.spells = [{ id: 1, name: 'Melee', icon: null }];
    timeline.events = [
      { spellId: 1, timestamp: 12000, duration: null, kind: 'cast' },
      { spellId: 1, timestamp: 12000, duration: null, kind: 'cast' },
      { spellId: 1, timestamp: 12500, duration: null, kind: 'cast' }
    ];
    const { container } = render(TimelineMagnifier, {
      magnifier: {
        timeline,
        timestamp: 11900,
        start: 10000,
        span: 10000,
        previewPixelsPerMillisecond: 0.1,
        x: 200,
        y: 400,
        zoom: 800
      },
      inspected: null,
      hidden: new Set<number>(),
      castSpellIds: new Set([1]),
      showCasts: true,
      showBuffs: true,
      showDeaths: true
    });
    expect(container.querySelectorAll('.event-details li')).toHaveLength(2);
    expect(container.querySelector('.event-details')).not.toHaveTextContent('TWIST');
    expect(container.querySelector('.event-details')).not.toHaveTextContent('0:12.5');
  });

  it('shows simultaneous melee attacks and their individual damage in separate rows', () => {
    const timeline = response(rows[0].report);
    timeline.spells = [
      { id: 1, name: 'Melee', icon: null },
      { id: 2, name: 'Holy Shock', icon: null }
    ];
    timeline.events = [
      {
        spellId: 1,
        timestamp: 12000,
        duration: null,
        kind: 'cast',
        castResults: [{ targetId: 8, targetName: 'Boss', kind: 'damage', amount: 172 }]
      },
      {
        spellId: 1,
        timestamp: 12000,
        duration: null,
        kind: 'cast',
        castResults: [{ targetId: 8, targetName: 'Boss', kind: 'damage', amount: 256 }]
      },
      { spellId: 2, timestamp: 12000, duration: null, kind: 'cast' },
      { spellId: 2, timestamp: 12000, duration: null, kind: 'cast' },
      { spellId: 1, timestamp: 12500, duration: null, kind: 'cast' }
    ];
    const { container } = render(TimelineMagnifier, {
      magnifier: {
        timeline,
        timestamp: 12000,
        start: 10000,
        span: 10000,
        x: 200,
        y: 400,
        zoom: 800
      },
      inspected: null,
      hidden: new Set<number>(),
      castSpellIds: new Set([1, 2]),
      showCasts: true,
      showBuffs: true,
      showDeaths: true
    });
    expect(container.querySelectorAll('.event-details li')).toHaveLength(4);
    expect(container.querySelectorAll('.event-details li')[0]).toHaveTextContent(
      '0:12 Melee -172 to Boss'
    );
    expect(container.querySelectorAll('.event-details li')[1]).toHaveTextContent(
      '0:12 Melee -256 to Boss'
    );
    expect(container.querySelector('.event-details')).not.toHaveTextContent('TWIST');
    expect(container.querySelectorAll('.magnified-event')).toHaveLength(5);
  });

  it('lists the hovered instant cast and its time without including other casts of that spell', async () => {
    const timeline = response(rows[0].report);
    timeline.spells = [{ id: 1, name: 'Holy Shock', icon: 'spell_holy_holybolt.jpg' }];
    timeline.events = [
      { spellId: 1, timestamp: 12000, duration: null, kind: 'cast' },
      { spellId: 1, timestamp: 12500, duration: null, kind: 'cast' }
    ];
    const props = {
      magnifier: {
        timeline,
        timestamp: 11900,
        hoveredEvent: timeline.events[0],
        start: 10000,
        span: 10000,
        x: 200,
        y: 400,
        zoom: 800
      },
      inspected: null,
      hidden: new Set<number>(),
      castSpellIds: new Set([1]),
      showCasts: true,
      showBuffs: true,
      showDeaths: true
    };
    const { container, rerender } = render(TimelineMagnifier, props);
    expect(container.querySelectorAll('.event-details li')).toHaveLength(1);
    expect(container.querySelector('.event-details')).toHaveTextContent('0:12 Holy Shock');
    expect(container.querySelector('.event-details')).not.toHaveTextContent('Casting');
    expect(container.querySelectorAll('.cast-icon img')).toHaveLength(2);
    await rerender({ ...props, magnifier: { ...props.magnifier, hoveredEvent: undefined } });
    expect(container.querySelectorAll('.event-details li')).toHaveLength(0);
  });

  it('lists every active event without a six-event limit or overflow message', () => {
    const timeline = response(rows[0].report);
    timeline.events = Array.from({ length: 12 }, (_, index) => ({
      spellId: index + 1,
      timestamp: 10000,
      duration: 10000,
      kind: 'buff' as const
    }));
    const { container } = render(TimelineMagnifier, {
      magnifier: {
        timeline,
        timestamp: 12000,
        start: 10000,
        span: 10000,
        x: 200,
        y: 800,
        zoom: 800
      },
      inspected: null,
      hidden: new Set<number>(),
      castSpellIds: new Set<number>(),
      showCasts: true,
      showBuffs: true,
      showDeaths: true
    });
    expect(container.querySelectorAll('.event-details li')).toHaveLength(12);
    expect(screen.getByRole('tooltip')).not.toHaveTextContent('more active events');
    expect(container.querySelector('.event-details')).toHaveTextContent('Spell 12');
  });

  it('shows every preview marker but lists only buffs and casts overlapping the cursor', () => {
    const timeline = response(rows[0].report);
    timeline.events = [
      { spellId: 1, timestamp: 11000, duration: 3000, kind: 'buff' },
      { spellId: 2, timestamp: 10000, duration: 2000, kind: 'buff' },
      { spellId: 3, timestamp: 13000, duration: 3000, kind: 'buff' },
      { spellId: 4, timestamp: 12100, castStart: 11900, duration: 200, kind: 'cast' },
      { spellId: 5, timestamp: 11000, duration: null, kind: 'cast' },
      { spellId: 6, timestamp: 12300, duration: null, kind: 'cast' }
    ];
    const { container } = render(TimelineMagnifier, {
      magnifier: {
        timeline,
        timestamp: 12000,
        start: 10000,
        span: 10000,
        previewPixelsPerMillisecond: 0.1,
        x: 200,
        y: 400,
        zoom: 800
      },
      inspected: null,
      hidden: new Set<number>(),
      castSpellIds: new Set<number>(),
      showCasts: true,
      showBuffs: true,
      showDeaths: true
    });
    expect(container.querySelectorAll('.magnified-event')).toHaveLength(6);
    expect(container.querySelector('.buff')).toHaveAttribute(
      'aria-label',
      expect.stringContaining('Spell 1')
    );
    expect(container.querySelector('.cast')).toHaveAttribute(
      'aria-label',
      expect.stringContaining('Spell 4')
    );
    expect(container.querySelectorAll('.event-details li')).toHaveLength(2);
    expect(container.querySelector('.cast-window')).toHaveStyle({ left: '19%', width: '2%' });
    const details = container.querySelector('.event-details')!;
    expect(details).toHaveTextContent('Spell 1');
    expect(details).toHaveTextContent('Spell 4');
    expect(details).toHaveTextContent('(0.2s) Spell 4');
    expect(details).not.toHaveTextContent('uptime');
    expect(details).not.toHaveTextContent('measured');
    expect(details).not.toHaveTextContent('Cast started');
    const listed = container.querySelectorAll('.event-details li');
    expect(listed[0]).toHaveTextContent('Buffs');
    expect(listed[0]).toHaveTextContent('Spell 1');
    expect(listed[1]).toHaveTextContent('Abilities');
    expect(listed[1]).toHaveTextContent('(0.2s) Spell 4');
    for (const id of [2, 3, 5, 6]) expect(details).not.toHaveTextContent(`Spell ${id}`);
  });

  it('shows measured cast times without Casting or Channeling prefixes', async () => {
    const timeline = response(rows[0].report);
    timeline.spells = [
      { id: 1, name: 'Holy Light', icon: null },
      { id: 2, name: 'Holy Shock', icon: null }
    ];
    timeline.events = [
      { spellId: 1, timestamp: 13000, castStart: 10700, duration: 2300, kind: 'cast' },
      { spellId: 2, timestamp: 13000, duration: null, kind: 'cast' }
    ];
    const props = {
      magnifier: {
        timeline,
        timestamp: 12000,
        start: 10000,
        span: 10000,
        x: 200,
        y: 400,
        zoom: 800
      },
      inspected: null,
      hidden: new Set<number>(),
      castSpellIds: new Set<number>(),
      showCasts: true,
      showBuffs: true,
      showDeaths: true
    };
    const { container, rerender } = render(TimelineMagnifier, props);
    expect(container.querySelector('.event-details')).toHaveTextContent(
      '0:10.7 - 0:13 (2.3s) Holy Light'
    );
    expect(container.querySelector('.event-details')).not.toHaveTextContent(/Casting|Channeling/);
    await rerender({ ...props, magnifier: { ...props.magnifier, timestamp: 13000 } });
    expect(container.querySelector('.event-details')).not.toHaveTextContent('Casting');
    expect(container.querySelector('.event-details')).toHaveTextContent('Holy Shock');
  });

  it('places benefits and measured cast windows on the same bar', () => {
    const timeline = response(rows[0].report);
    timeline.events = [
      { spellId: 1, timestamp: 12000, castStart: 11000, duration: 1000, kind: 'cast' },
      { spellId: 2, timestamp: 12000, duration: 5000, kind: 'buff' },
      { spellId: 3, timestamp: 12000, duration: 5000, kind: 'buff' }
    ];
    const { container } = render(TimelineMagnifier, {
      magnifier: {
        timeline,
        events: timeline.events,
        start: 10000,
        span: 10000,
        x: 200,
        y: 160,
        zoom: 800
      },
      inspected: null,
      hidden: new Set<number>(),
      castSpellIds: new Set<number>(),
      showCasts: true,
      showBuffs: true,
      showDeaths: true
    });
    const abilityTop = parseFloat(container.querySelector<HTMLElement>('.cast')!.style.top);
    for (const buff of container.querySelectorAll<HTMLElement>('.buff'))
      expect(parseFloat(buff.style.top)).toBe(47);
    expect(container.querySelector('.cast-window')).toHaveStyle({ top: '47px' });
    expect(47 - (abilityTop + 22)).toBe(6);
    expect(container.querySelector('.cast-icon')).toBeInTheDocument();
    expect(container.querySelector('.cast-connectors')).toHaveStyle({
      left: '10%',
      width: '10%',
      top: '19px'
    });
    expect(container.querySelector('.cast-connectors')).not.toHaveClass('instant');
  });

  it('zooms time spacing while keeping icons and markers at their normal size', () => {
    const timeline = response(rows[0].report);
    timeline.events = [
      { spellId: 1, timestamp: 10000, duration: null, kind: 'cast' },
      { spellId: 2, timestamp: 13000, duration: null, kind: 'cast' },
      { spellId: 2, timestamp: 13000, duration: 5000, kind: 'buff' }
    ];
    timeline.spells = [
      { id: 1, name: 'First', icon: 'spell_holy_holybolt.jpg' },
      { id: 2, name: 'Second', icon: 'spell_holy_holybolt.jpg' }
    ];
    const { container } = render(TimelineMagnifier, {
      magnifier: {
        timeline,
        events: timeline.events,
        start: 10000,
        span: 10000,
        x: 200,
        y: 160,
        zoom: 200,
        showIcons: false,
        previewPixelsPerMillisecond: 0.0342
      },
      inspected: null,
      hidden: new Set<number>(),
      castSpellIds: new Set([1, 2]),
      showCasts: true,
      showBuffs: true,
      showDeaths: true
    });
    const casts = container.querySelectorAll<HTMLElement>('.magnified-event.cast');
    expect(casts).toHaveLength(2);
    expect(casts[0]).toHaveStyle({ left: '0%', top: '19px', width: '22px' });
    expect(container.querySelectorAll('.cast-connectors.instant')).toHaveLength(2);
    expect(container.querySelector('.cast-connectors.instant')).toHaveStyle({ width: '0%' });
    expect(casts[1]).toHaveStyle({ left: '30%', top: '19px', width: '22px' });
    expect(container.querySelector('.buff')).toHaveStyle({
      left: '30%',
      top: '47px',
      width: 'max(2px, 50%)'
    });
    expect(container.querySelector('img')).toBeNull();
    expect(container.querySelector('.magnified-track')).toHaveStyle({ height: '68px' });
  });

  it('keeps event positions stable when earlier spells are outside the preview', () => {
    const timeline = response(rows[0].report);
    timeline.events = [
      { spellId: 1, timestamp: 0, duration: null, kind: 'cast' },
      { spellId: 2, timestamp: 12000, duration: null, kind: 'cast' }
    ];
    const { container } = render(TimelineMagnifier, {
      magnifier: {
        timeline,
        events: timeline.events,
        start: 10000,
        span: 10000,
        x: 200,
        y: 160,
        zoom: 200
      },
      inspected: null,
      hidden: new Set<number>(),
      castSpellIds: new Set<number>(),
      showCasts: true,
      showBuffs: true,
      showDeaths: true
    });
    expect(container.querySelectorAll('.magnified-event')).toHaveLength(1);
    expect(container.querySelector('.magnified-event')).toHaveStyle({ top: '19px' });
  });
  it('uses the row event snapshot without filtering or spell-hover dimming a second time', async () => {
    const timeline = response(rows[0].report);
    timeline.events = [
      { spellId: 31884, timestamp: 12000, duration: null, kind: 'cast' },
      { spellId: 31884, timestamp: 12000, duration: 5000, kind: 'buff' },
      { spellId: 31884, timestamp: 12000, duration: null, kind: 'heal', amount: 100 },
      { spellId: 31884, timestamp: 12000, duration: null, kind: 'mana-returned', amount: 100 }
    ];
    const props = {
      magnifier: {
        timeline,
        events: timeline.events.filter((event) => event.kind === 'cast' || event.kind === 'buff'),
        start: 10000,
        span: 10000,
        x: 200,
        y: 160,
        zoom: 200
      },
      inspected: null,
      hidden: new Set<number>(),
      castSpellIds: new Set<number>(),
      showCasts: true,
      showBuffs: true,
      showDeaths: true,
      showDirectHeals: true,
      showManaSpent: true,
      showManaReceived: true
    };
    const { container, rerender } = render(TimelineMagnifier, props);
    expect(container.querySelectorAll('.magnified-event')).toHaveLength(2);
    expect(container.querySelector('.healing, .mana-event')).toBeNull();
    await rerender({ ...props, showCasts: false });
    expect(container.querySelectorAll('.magnified-event')).toHaveLength(2);
    await rerender({
      ...props,
      hidden: new Set([31884]),
      inspected: { id: 999, name: 'Other spell', color: '#fff', detail: '' }
    });
    expect(container.querySelectorAll('.magnified-event')).toHaveLength(2);
    expect(container.querySelector('.spell-dimmed, .spell-highlighted')).toBeNull();
  });

  it.each(['health', 'mana'] as const)(
    'uses the %s row events and shows precise resource details despite main timeline filters',
    (resourceMode) => {
      const timeline = {
        ...response(rows[0].report),
        maxHealth: 12000,
        healthCapacities: [{ timestamp: 0, maxHealth: 12000, estimatedMaxHealth: null }],
        maxMana: 5500,
        spells: [
          { id: 1, name: resourceMode === 'mana' ? 'Mana Potion' : 'Healing Potion', icon: null }
        ],
        events: [
          {
            spellId: 1,
            timestamp: 12345,
            duration: null,
            amount: 1250,
            kind: resourceMode === 'mana' ? ('mana-returned' as const) : ('heal' as const)
          }
        ]
      };
      const { container } = render(TimelineMagnifier, {
        magnifier: {
          timeline,
          events: timeline.events,
          resourceMode,
          timestamp: 12345,
          phases: [{ id: 2, timestamp: 15000 }],
          start: 10000,
          span: 10000,
          x: 200,
          y: 160,
          zoom: 200
        },
        inspected: null,
        hidden: new Set([1]),
        castSpellIds: new Set<number>(),
        showCasts: false,
        showBuffs: false,
        showDeaths: false,
        showDirectHeals: resourceMode === 'health',
        showManaSpent: resourceMode === 'mana',
        showManaReceived: resourceMode === 'mana'
      });
      expect(screen.getByRole('tooltip')).toHaveClass(resourceMode);
      expect(screen.getByText('At 0:12.35')).toBeInTheDocument();
      expect(
        screen.getByText(resourceMode === 'mana' ? 'Max mana: 5,500' : 'Health 12,000')
      ).toBeInTheDocument();
      expect(container.querySelector('.event-details')).toHaveTextContent(
        `0:12.35 ${timeline.spells[0].name} +1,250`
      );
      expect(container.querySelectorAll('.magnified-event')).toHaveLength(1);
      expect(
        parseFloat(container.querySelector<HTMLElement>('.preview-cursor')!.style.left)
      ).toBeCloseTo(23.45);
      expect(screen.getByText('P2')).toBeInTheDocument();
    }
  );

  it('clips intersecting buffs and distinguishes markers, deaths, and missing spell metadata', async () => {
    const timeline = response(rows[0].report);
    timeline.events = [
      { spellId: 31884, timestamp: 0, duration: 15000, kind: 'buff' },
      { spellId: 31884, timestamp: 15000, duration: null, kind: 'marker' },
      { spellId: 999, timestamp: 15000, duration: null, kind: 'cast' },
      { spellId: 0, timestamp: 18000, duration: null, kind: 'death' },
      { spellId: 31884, timestamp: 30000, duration: null, kind: 'cast' }
    ];
    const props = {
      magnifier: { timeline, start: 10000, span: 10000, x: 10, y: 10, zoom: 300 },
      inspected: null,
      hidden: new Set<number>(),
      castSpellIds: new Set<number>(),
      showCasts: true,
      showBuffs: true,
      showDeaths: true
    };
    const { container, rerender } = render(TimelineMagnifier, props);
    expect(container.querySelectorAll('.magnified-event')).toHaveLength(4);
    expect(container.querySelector('.buff')).toHaveStyle({
      left: '-100%',
      width: 'max(2px, 150%)',
      top: '47px'
    });
    expect(screen.getByLabelText('Inferred marker')).toBeInTheDocument();
    expect(container.querySelector('.death')).toBeInTheDocument();
    expect(container.querySelectorAll('.spell-dimmed')).toHaveLength(0);
    await rerender({ ...props, showBuffs: false, showDeaths: false });
    expect(container.querySelectorAll('.magnified-event')).toHaveLength(2);
    expect(container.querySelector('.buff, .death')).toBeNull();
  });
  it('renders a clipped zoom preview with its time bounds and event visibility options', async () => {
    const timeline = response(rows[0].report);
    const props = {
      magnifier: { timeline, start: 10000, span: 10000, x: 200, y: 160, zoom: 200 },
      inspected: { id: 31884, name: 'Avenging Wrath', color: '#fff', detail: 'test' },
      hidden: new Set<number>(),
      castSpellIds: new Set([31884]),
      showCasts: true,
      showBuffs: true,
      showDeaths: true
    };
    const { container, rerender } = render(TimelineMagnifier, props);
    expect(screen.getByRole('tooltip').getAttribute('style')).toContain('20px');
    expect(screen.getByRole('tooltip').style.bottom).toBe('');
    expect(screen.getByRole('tooltip').style.top).toBe('152px');
    expect(container.querySelector('.magnifier-heading')).toHaveTextContent(
      'Player0 Timeline 200%'
    );
    expect(container.querySelector('.magnifier-heading')).not.toHaveTextContent('Avenging Wrath');
    expect(screen.getByRole('tooltip')).not.toHaveTextContent('·');
    expect(container.querySelector('.magnifier-zoom')).toHaveTextContent('200%');
    expect(container.querySelector('.magnifier-times span:first-child')).toHaveTextContent('0:10');
    expect(container.querySelector('.magnifier-times span:last-child')).toHaveTextContent('0:20');
    expect(container.querySelectorAll('.magnified-event')).toHaveLength(2);
    expect(container.querySelector('.magnified-event.spell-dimmed')).toBeNull();
    expect(container.querySelector('.magnified-event.buff')).toHaveClass('power-window');
    expect(container.querySelector('.spell-highlighted')).toBeNull();
    expect(container.querySelector('.magnified-event.buff')?.getAttribute('style')).toContain(
      'width: max(2px, 200%)'
    );
    await rerender({ ...props, showCasts: false });
    expect(container.querySelectorAll('.magnified-event')).toHaveLength(1);
    await rerender({ ...props, hidden: new Set([31884]) });
    expect(container.querySelectorAll('.magnified-event')).toHaveLength(0);
    await rerender({ ...props, magnifier: null });
    expect(screen.queryByRole('tooltip')).toBeNull();
  });
});
