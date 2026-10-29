import { describe, expect, it } from 'vitest';
import type { ReportTimeline } from './models/ReportTimeline';
import {
  eventLanes,
  castIntervals,
  isPowerWindow,
  spellAppearance,
  spellIconUrl,
  timelineSpellGroups,
  timelineTime,
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
    expect(eventLanes(timeline.events).map(({ event }) => event.timestamp)).toEqual([0, 0, 230000]);
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
