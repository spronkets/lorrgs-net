import { describe, expect, it } from 'vitest';
import type { RankingReport } from './models/RankingReport';
import type { ReportTimeline } from './models/ReportTimeline';
import { buildRaidNote, specNoteTag } from './raidNote';
import type { RaidNoteOptions } from './models/RaidNoteOptions';
import { rows, response, bossResponse } from './testing/timelineFixtures';

const report: RankingReport = {
  ...rows[0].report,
  fights: [
    {
      ...rows[0].report.fights[0],
      encounterId: 3455,
      name: 'Vashnik the Malignant',
      phases: [
        { id: 1, timestamp: 0 },
        { id: 2, timestamp: 60000 }
      ]
    }
  ]
};
const timeline: ReportTimeline = {
  ...response(report),
  events: [
    { kind: 'cast', spellId: 31884, timestamp: 108750, duration: null },
    { kind: 'cast', spellId: 343527, timestamp: 24500, duration: null },
    { kind: 'cast', spellId: 255937, timestamp: 25150, duration: null },
    { kind: 'buff', spellId: 31884, timestamp: 24500, duration: 20000 },
    { kind: 'death', spellId: 0, timestamp: 200000, duration: null }
  ]
};
const options: RaidNoteOptions = {
  format: 'mrt',
  target: 'name',
  playerName: '',
  dynamic: false,
  difficulty: 'mythic',
  bossName: 'Vashnik the Malignant',
  specSlug: 'retribution-paladin'
};
function note(overrides: Partial<RaidNoteOptions> = {}, hidden = new Set<number>()) {
  return buildRaidNote(timeline, report, hidden, { ...options, ...overrides });
}

describe('raid note export', () => {
  it('never exports inferred events as player casts', () => {
    const sample: ReportTimeline = {
      ...timeline,
      events: [
        timeline.events[0],
        { kind: 'cast', spellId: 370537, timestamp: 1000, duration: 30000, isInferred: true },
        { kind: 'marker', spellId: 267134, timestamp: 2000, duration: 0, isInferred: true }
      ]
    };
    const output = buildRaidNote(sample, report, new Set(), options);
    expect(output.count).toBe(1);
    expect(output.text).not.toMatch(/370537|267134/);
  });

  it('uses observed addon anchors, not fractional intermission IDs or corrected display labels', () => {
    const sample = {
      ...report,
      bossTimeline: {
        ...bossResponse(report),
        phases: [
          { id: 1, timestamp: 0, notePhaseId: 1 },
          { id: 1.5, timestamp: 20000, isIntermission: true },
          { id: 1.75, timestamp: 45000, isIntermission: true },
          { id: 2, timestamp: 90000, notePhaseId: 3 }
        ]
      }
    };
    const output = buildRaidNote(timeline, sample, new Set(), { ...options, dynamic: true });
    expect(output.text).toContain('{time:00:24.5}');
    expect(output.text).toContain('{time:00:18.8,p3}');
    expect(output.text).not.toMatch(/p1\\.5|p1\\.75|p2/);
    expect(
      buildRaidNote(timeline, sample, new Set(), {
        ...options,
        dynamic: true,
        format: 'nsrt'
      }).text
    ).toContain('ph:3;time:18.8;');
  });
  it('matches MRT syntax with chronological casts and tenth-second rounding', () => {
    expect(note()).toEqual({
      text: '{time:00:24.5} - {spell:343527}\n{time:00:25.2} - {spell:255937}\n{time:01:48.8} - {spell:31884}',
      count: 3,
      error: ''
    });
    expect(note({ playerName: 'MyPaladin' }).text).toContain('- MyPaladin {spell:343527}');
    expect(note({ target: 'spec' }).text).toContain('- spec:PALADIN:3 {spell:343527}');
  });

  it('matches NSRT metadata, spec tags and absolute seconds', () => {
    expect(note({ format: 'nsrt', target: 'spec' }).text).toBe(
      'EncounterID:3455;Difficulty:Mythic;Name:Vashnik the Malignant;\n' +
        'ph:1;time:24.5;tag:70;spellid:343527;\n' +
        'ph:1;time:25.2;tag:70;spellid:255937;\n' +
        'ph:1;time:108.8;tag:70;spellid:31884;'
    );
    expect(note({ format: 'nsrt', playerName: 'Nickname' }).text).toContain('tag:Nickname;');
    expect(note({ format: 'nsrt' }).text).not.toContain('tag:');
  });

  it('uses the player fight phases, including casts exactly at a phase boundary', () => {
    expect(note({ dynamic: true }).text).toContain('{time:00:48.8,p2}');
    expect(note({ dynamic: true, format: 'nsrt' }).text).toContain('ph:2;time:48.8;');
    const boundary = { ...timeline, events: [{ ...timeline.events[0], timestamp: 60000 }] };
    expect(buildRaidNote(boundary, report, new Set(), { ...options, dynamic: true }).text).toBe(
      '{time:00:00.0,p2} - {spell:31884}'
    );
  });

  it('uses bundled boss phases only for that same fight and otherwise uses pull times', () => {
    const noPhases = { ...report, fights: [{ ...report.fights![0], phases: undefined }] };
    expect(
      buildRaidNote(timeline, { ...noPhases, bossTimeline: bossResponse(report) }, new Set(), {
        ...options,
        dynamic: true
      }).text
    ).toContain(',p2}');
    expect(
      buildRaidNote(timeline, noPhases, new Set(), { ...options, dynamic: true }).text
    ).not.toContain(',p2}');
    expect(
      buildRaidNote(
        timeline,
        { ...noPhases, bossTimeline: { ...bossResponse(report), fightId: 99 } },
        new Set(),
        { ...options, dynamic: true }
      ).text
    ).not.toContain(',p2}');
  });

  it('honors hidden spells and reports empty selections rather than exporting a misleading note', () => {
    expect(note({}, new Set([31884])).count).toBe(2);
    expect(note({}, new Set([31884])).text).not.toContain('31884');
    expect(note({}, new Set([31884, 343527, 255937])).error).toBe(
      'No selected cast events to copy.'
    );
  });

  it('rejects missing encounter or specialization metadata instead of inventing it', () => {
    const missing = { ...report, fights: [{ ...report.fights![0], encounterId: 0 }], players: [] };
    expect(
      buildRaidNote(timeline, missing, new Set(), { ...options, format: 'nsrt' }).error
    ).toContain('Encounter ID is unavailable');
    expect(
      buildRaidNote(timeline, missing, new Set(), { ...options, target: 'spec' }).error
    ).toContain('specialization');
    expect(specNoteTag(0, 'nsrt')).toBeNull();
    expect(specNoteTag(269, 'mrt')).toBe('spec:MONK:3');
    expect(specNoteTag(105, 'mrt')).toBe('spec:DRUID:4');
  });

  it('does not inject note tokens through a custom player name', () => {
    expect(note({ playerName: 'A;{spell:1}\nB' }).text).toContain('Aspell:1B {spell:343527}');
  });
});
