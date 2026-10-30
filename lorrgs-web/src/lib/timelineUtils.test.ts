import { describe, expect, it } from 'vitest';
import type { ReportTimeline } from './models/ReportTimeline';
import {
  castIntervals,
  isPowerWindow,
  spellAppearance,
  spellIconUrl,
  timelineSpellGroups,
  timelineTime,
  preciseTimelineTime,
  timelineEventDetail,
  resourceEventColor,
  visibleTimelineEvents
} from './timelineUtils';

const timeline: ReportTimeline = {
  reportCode: 'Fixture',
  fightId: 1,
  sourceId: 7,
  playerName: 'Alice',
  duration: 240000,
  isComplete: true,
  spells: [{ id: 31884, name: 'Avenging Wrath', icon: 'spell_holy_avenginewrath.jpg' }],
  events: [
    { spellId: 31884, timestamp: 0, duration: null, kind: 'cast' },
    { spellId: 31884, timestamp: 0, duration: 20000, kind: 'buff' },
    { spellId: 0, timestamp: 230000, duration: null, kind: 'death' }
  ]
};

describe('aligned timeline utilities', () => {
  it('colors recorded event semantics before spell names and colors healing casts without results', () => {
    const cast = { spellId: 1, timestamp: 1000, duration: null, kind: 'cast' as const };
    const spell = { id: 1, name: 'Chain Heal (Rank 5)', icon: null };
    expect(resourceEventColor(cast, spell)).toBe('#69d98b');
    expect(resourceEventColor({ ...cast, kind: 'damage' }, spell)).toBe('#ff6969');
    expect(resourceEventColor({ ...cast, kind: 'mana-spent' }, spell)).toBe('#d9ad69');
    expect(resourceEventColor({ ...cast, kind: 'mana-returned' }, spell)).toBe('#69aaff');
    expect(resourceEventColor({ ...cast, castResults: [
      { targetId: 2, targetName: 'Ally', kind: 'heal', amount: 100 }
    ] }, { ...spell, name: 'Unknown Heal' })).toBe('#69d98b');
    expect(resourceEventColor({ ...cast, castResults: [
      { targetId: 2, targetName: 'Boss', kind: 'damage', amount: 100 }
    ] }, { ...spell, name: 'Shadow Bolt' })).toBe('#ff6969');
  });
  it('uses amber for mana spent and keeps mana gains blue in either lane', () => {
    for (const displayBelow of [false, true]) {
      const event = { spellId: 1, timestamp: 1000, duration: null, displayBelow };
      const spell = { id: 1, name: 'Mana Potion', icon: null };
      expect(resourceEventColor({ ...event, kind: 'mana-spent' }, spell)).toBe('#d9ad69');
      expect(resourceEventColor({ ...event, kind: 'mana-returned' }, spell)).toBe('#69aaff');
    }
  });
  it.each([
    [false, false, []],
    [true, false, ['mana-spent']],
    [false, true, ['mana-returned']],
    [true, true, ['mana-spent', 'mana-returned']]
  ] as const)('filters mana spent=%s and received=%s independently', (spent, received, kinds) => {
    const sample: ReportTimeline = { ...timeline, events: [
      { spellId: 1, timestamp: 1000, duration: null, kind: 'mana-spent' },
      { spellId: 2, timestamp: 2000, duration: null, kind: 'mana-returned' }
    ] };
    expect(visibleTimelineEvents(sample, new Set(), false, false, false,
      false, false, false, spent, received).map((event) => event.kind)).toEqual(kinds);
  });
  it('rounds timestamps to at most two decimals with correct minute carry', () => {
    expect(preciseTimelineTime(79644)).toBe('1:19.64');
    expect(preciseTimelineTime(80728)).toBe('1:20.73');
    expect(preciseTimelineTime(59999)).toBe('1:00');
    expect(preciseTimelineTime(10700)).toBe('0:10.7');
    expect(preciseTimelineTime(0)).toBe('0:00');
  });
  it('shows heal health transitions with explicit snapshot provenance and unknown values', () => {
    const heal = {
      spellId: 1, timestamp: 1000, duration: null, kind: 'heal' as const,
      sourceName: 'Healer', healthBeforePercent: 40, healthAfterPercent: 70.24
    };
    expect(timelineEventDetail(heal, undefined)).toContain('(70.24%, +30.24%, by Healer)');
    expect(timelineEventDetail({ ...heal, healthBeforeIsPreviousSnapshot: true }, undefined))
      .toContain('(70.24%, +30.24%, by Healer)');
    expect(timelineEventDetail({ ...heal, healthBeforePercent: null }, undefined))
      .toContain('(70.24%, unknown change, by Healer)');
    expect(timelineEventDetail({ ...heal, healthBeforePercent: 0 }, undefined))
      .toContain('(70.24%, +70.24%, by Healer)');
    expect(timelineEventDetail({ ...heal, healthBeforePercent: null, healthAfterPercent: null }, undefined))
      .toContain('(unknown, unknown change, by Healer)');
    expect(timelineEventDetail({
      ...heal, kind: 'damage', amount: 1500, healthBeforePercent: 70, healthAfterPercent: 55
    }, { id: 1, name: 'Melee', icon: null })).toContain('Melee · -1,500 · (55%, -15%, by Healer)');
  });
  it('colors health changes and consumables and identifies the source in details', () => {
    const heal = { spellId: 1, timestamp: 1000, duration: null, kind: 'heal' as const, sourceName: 'Healer' };
    expect(resourceEventColor(heal, { id: 1, name: 'Holy Light', icon: null })).toBe('#69d98b');
    expect(resourceEventColor({ ...heal, kind: 'damage' }, undefined)).toBe('#ff6969');
    expect(resourceEventColor(heal, { id: 1, name: 'Super Healing Potion', icon: null })).toBe('#69d98b');
    expect(resourceEventColor({ ...heal, kind: 'cast', displayBelow: false },
      { id: 1, name: 'Evocation', icon: null })).toBe('#69aaff');
    expect(timelineEventDetail(heal, undefined)).toContain('by Healer');
  });
  it('omits inferred wording for buffs without claiming a measured end time', () => {
    const event = { spellId: 31884, timestamp: 0, duration: 20000, kind: 'buff' as const, isInferred: true };
    const detail = timelineEventDetail(event, timeline.spells[0]);
    expect(detail).not.toContain('inferred');
    expect(detail).not.toContain('measured');
    expect(detail).toContain('displayed uptime');
    expect(event.isInferred).toBe(true);
  });
  it('clips inferred prepull intervals for display and treats markers as cast-layer annotations', () => {
    const sample: ReportTimeline = {
      ...timeline,
      events: [
        { kind: 'buff', spellId: 370537, timestamp: -4000, duration: 30000, isInferred: true },
        { kind: 'marker', spellId: 267134, timestamp: 10000, duration: 0, isInferred: true }
      ]
    };
    expect(visibleTimelineEvents(sample, new Set(), true, true, true)[0]).toMatchObject({
      timestamp: 0,
      duration: 26000,
      isInferred: true
    });
    expect(visibleTimelineEvents(sample, new Set(), false, true, true)).toHaveLength(1);
    expect(sample.events[0].timestamp).toBe(-4000);
  });
  it('measures consecutive casts only, sorting and deduplicating timestamps without inventing edge gaps', () => {
    expect(
      castIntervals(
        [
          ...timeline.events,
          { spellId: 31884, timestamp: 120500, duration: null, kind: 'cast' },
          { spellId: 31884, timestamp: 120500, duration: null, kind: 'cast' },
          { spellId: 31884, timestamp: 60000, duration: null, kind: 'cast' },
          { spellId: 642, timestamp: 20000, duration: null, kind: 'cast' }
        ],
        31884
      )
    ).toEqual([
      { start: 0, end: 60000, label: '60s' },
      { start: 60000, end: 120500, label: '60.5s' }
    ]);
    expect(castIntervals(timeline.events, 31884)).toEqual([]);
    expect(castIntervals(timeline.events, 2825)).toEqual([]);
  });

  it('assigns stable spell colors and labeled palettes without relying on class color', () => {
    const wrath = { id: 31884, name: 'Avenging Wrath', icon: null };
    const strike = { id: 35395, name: 'Crusader Strike', icon: null };
    const shield = { id: 642, name: 'Divine Shield', icon: null };
    expect(spellAppearance(wrath, true).group).toBe('power');
    expect(spellAppearance(strike, true).group).toBe('ability');
    expect(spellAppearance(shield, false).group).toBe('received');
    expect(spellAppearance(wrath, false).color).toBe(spellAppearance(wrath, true).color);
    expect(spellAppearance(wrath, true).color).not.toBe(spellAppearance(strike, true).color);
  });

  it('keeps received Bloodlust prominent without labeling it as a Paladin cast', () => {
    const sample: ReportTimeline = {
      ...timeline,
      spells: [
        ...timeline.spells,
        { id: 2825, name: 'Bloodlust', icon: null },
        { id: 27179, name: 'Seal of Blood', icon: null },
        { id: 12345, name: 'Received effect', icon: null }
      ],
      events: [
        ...timeline.events,
        { spellId: 2825, timestamp: 30000, duration: 40000, kind: 'buff' },
        { spellId: 27179, timestamp: 1000, duration: null, kind: 'cast' },
        { spellId: 12345, timestamp: 2000, duration: 5000, kind: 'buff' }
      ]
    };
    const groups = timelineSpellGroups([sample]);
    expect(
      groups.find((group) => group.label === 'DPS / power windows')?.spells.map((spell) => spell.id)
    ).toEqual([31884, 2825]);
    expect(
      groups.find((group) => group.label === 'Player abilities')?.spells.map((spell) => spell.id)
    ).toEqual([27179]);
    expect(
      groups
        .find((group) => group.label === 'Other effects on player')
        ?.spells.map((spell) => spell.id)
    ).toEqual([12345]);
    expect(isPowerWindow(sample.spells[1])).toBe(true);
    expect(visibleTimelineEvents(sample, new Set(), true, true, true)).toContainEqual(
      sample.events[3]
    );
  });

  it('keeps time zero visible and does not normalize timestamps to fight duration', () => {
    expect(timelineTime(0)).toBe('0:00');
    expect(timelineTime(125500)).toBe('2:05');
  });

  it('filters real event kinds and spell selection independently of death markers', () => {
    expect(visibleTimelineEvents(timeline, new Set([31884]), true, true, true)).toEqual([
      timeline.events[2]
    ]);
    expect(visibleTimelineEvents(timeline, new Set(), false, true, false)).toEqual([
      timeline.events[1]
    ]);
  });

  it('constructs icons only from valid filenames on the fixed icon host', () => {
    expect(spellIconUrl('spell_holy_avenginewrath.jpg')).toBe(
      'https://wow.zamimg.com/images/wow/icons/medium/spell_holy_avenginewrath.jpg'
    );
    expect(spellIconUrl('javascript:alert(1)')).toBeNull();
    expect(spellIconUrl(null)).toBeNull();
  });
});
