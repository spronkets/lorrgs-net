import type { BossTimeline } from '../models/BossTimeline';
import type { RankingReport } from '../models/RankingReport';
import type { ReportTimeline } from '../models/ReportTimeline';

export const rows = Array.from({ length: 5 }, (_, index) => ({
  rank: index + 1,
  report: {
    reportId: `Report${index}`,
    title: `Report ${index}`,
    duration: 240000,
    percentile: 99,
    startTime: '2026-10-22T12:00:00Z',
    fights: [
      {
        fightId: index + 1,
        name: 'Boss',
        duration: 240000,
        isKill: true,
        startTime: '',
        percentile: null
      }
    ],
    players: [
      {
        name: `Player${index}`,
        playerId: 0,
        classId: 2,
        specId: 70,
        specSlug: 'retribution-paladin',
        performance: 100
      }
    ]
  } satisfies RankingReport
}));
export function response(report: RankingReport): ReportTimeline {
  return {
    reportCode: report.reportId,
    fightId: report.fights![0].fightId,
    sourceId: 7,
    playerName: report.players![0].name,
    duration: 240000,
    isComplete: true,
    spells: [{ id: 31884, name: 'Avenging Wrath', icon: 'spell_holy_avenginewrath.jpg' }],
    events: [
      { spellId: 31884, timestamp: 10000, duration: null, kind: 'cast' },
      { spellId: 31884, timestamp: 10000, duration: 20000, kind: 'buff' }
    ]
  };
}

export function bossResponse(report: RankingReport): BossTimeline {
  return {
    reportCode: report.reportId,
    fightId: report.fights![0].fightId,
    name: report.fights![0].name,
    duration: 240000,
    isComplete: true,
    spells: [{ id: 123, name: 'Boss Strike', icon: 'spell_fire_fireball.jpg' }],
    events: [{ spellId: 123, timestamp: 30000, duration: null, kind: 'cast' }],
    phases: [
      { id: 1, timestamp: 0 },
      { id: 2, timestamp: 60000 }
    ]
  };
}
