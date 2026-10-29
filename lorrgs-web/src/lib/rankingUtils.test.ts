import { describe, expect, it } from 'vitest';
import {
  formatRankingDuration,
  getDisplayPercentile,
  getTopPercentLabel,
  getRankingFilterError,
  getRankingReportLink,
  getRotationAnalyzerLink,
  parseKillTime,
  toKillTimeInput,
  selectRankingReports,
  summarizeRankingReports
} from './rankingUtils';
import type { RankingFilters } from './models/rankings/RankingFilters';

const filters: RankingFilters = {
  region: '',
  minDuration: '',
  maxDuration: '',
  fromDate: '',
  toDate: ''
};
const reports = [
  {
    duration: 240000,
    startTime: '2026-10-20T12:00:00',
    players: [{ performance: 100, serverRegion: 'NA' }]
  },
  {
    duration: 300000,
    startTime: '2026-10-21T23:59:59.999',
    players: [{ performance: 200, serverRegion: 'EU' }]
  },
  {
    duration: 360000,
    startTime: '2026-10-22T00:00:00',
    players: [{ performance: 0, serverRegion: 'us' }]
  }
];

describe('ranking utilities', () => {
  it('formats legacy kill-time links as minute and second duration inputs', () => {
    expect(toKillTimeInput('120')).toBe('2:00');
    expect(toKillTimeInput('2:05')).toBe('2:05');
    expect(toKillTimeInput('3605')).toBe('60:05');
    expect(toKillTimeInput('0')).toBe('0:00');
    expect(toKillTimeInput('')).toBe('');
    expect(toKillTimeInput('invalid')).toBe('invalid');
  });
  it('converts percentile to a top percentage without showing a misleading zero', () => {
    expect(getTopPercentLabel(99)).toBe('Top 1%');
    expect(getTopPercentLabel(95)).toBe('Top 5%');
    expect(getTopPercentLabel(98.5)).toBe('Top 2%');
    expect(getTopPercentLabel(100)).toBe('Top 1%');
    expect(getTopPercentLabel(99.99)).toBe('Top 1%');
    expect(getTopPercentLabel(0)).toBe('Top 100%');
    for (const value of [null, undefined, NaN, -1, 101])
      expect(getTopPercentLabel(value)).toBe('N/A');
  });
  it('formats percentile values with a safe fallback', () => {
    expect(getDisplayPercentile(81.234)).toBe('81.2%');
    expect(getDisplayPercentile(null)).toBe('N/A');
    expect(getDisplayPercentile(undefined)).toBe('N/A');
  });

  it('parses kill times with strict minute/second validation', () => {
    expect(parseKillTime('4:00')).toBe(240000);
    expect(parseKillTime('240')).toBe(240000);
    expect(parseKillTime('0:00')).toBe(0);
    expect(parseKillTime('')).toBeNull();
    for (const invalid of ['4:60', '-1', '4:1', '1.5', 'Infinity']) {
      expect(parseKillTime(invalid)).toBeNaN();
    }
    expect(getRankingFilterError({ ...filters, minDuration: '5:00', maxDuration: '4:00' })).toMatch(
      /Minimum/
    );
    expect(
      getRankingFilterError({ ...filters, fromDate: '2026-10-22', toDate: '2026-10-21' })
    ).toMatch(/From date/);
  });

  it('filters inclusive kill-time bounds and normalizes NA region without mutating reports', () => {
    expect(
      selectRankingReports(
        reports,
        { ...filters, region: 'us', minDuration: '4:00', maxDuration: '6:00' },
        'rank'
      ).map((entry) => entry.rank)
    ).toEqual([1, 3]);
    expect(reports.map((report) => report.duration)).toEqual([240000, 300000, 360000]);
  });

  it('includes the whole final local calendar day and excludes the next day', () => {
    const selected = selectRankingReports(
      reports,
      {
        ...filters,
        fromDate: '2026-10-21',
        toDate: '2026-10-21'
      },
      'rank'
    );
    expect(selected.map((entry) => entry.rank)).toEqual([2]);
  });

  it('sorts without losing original ranks and puts missing values last', () => {
    expect(
      selectRankingReports([...reports, {}], filters, 'performance').map((entry) => entry.rank)
    ).toEqual([2, 1, 3, 4]);
    expect(selectRankingReports(reports, filters, 'date').map((entry) => entry.rank)).toEqual([
      3, 2, 1
    ]);
    expect(selectRankingReports(reports, filters, 'duration').map((entry) => entry.rank)).toEqual([
      1, 2, 3
    ]);
    expect(selectRankingReports([{}], { ...filters, minDuration: '1' }, 'rank')).toEqual([]);
    expect(selectRankingReports(reports, { ...filters, maxDuration: 'invalid' }, 'rank')).toEqual(
      []
    );
  });

  it('summarizes only provided reports with correct medians and zero performance', () => {
    expect(summarizeRankingReports(reports)).toEqual({
      count: 3,
      bestPerformance: 200,
      medianDuration: 300000
    });
    expect(summarizeRankingReports(reports.slice(0, 2)).medianDuration).toBe(270000);
    expect(summarizeRankingReports(reports.slice(2)).bestPerformance).toBe(0);
    expect(summarizeRankingReports([])).toEqual({
      count: 0,
      bestPerformance: null,
      medianDuration: null
    });
    expect(formatRankingDuration(270000)).toBe('4:30');
    expect(formatRankingDuration(undefined)).toBe('N/A');
  });

  it('links to the correct edition, fight and known actor without inventing a source', () => {
    expect(
      getRankingReportLink(
        { reportId: 'AbC', fights: [{ fightId: 3 }], players: [{ playerId: 8 }] },
        'anniversary'
      )
    ).toBe('https://fresh.warcraftlogs.com/reports/AbC#fight=3&source=8');
    expect(getRankingReportLink({ reportId: 'AbC', players: [{ playerId: 0 }] }, 'era')).toBe(
      'https://vanilla.warcraftlogs.com/reports/AbC'
    );
    expect(getRankingReportLink({ reportId: 'AbC' }, 'mistsofpandaria')).toContain(
      'classic.warcraftlogs.com'
    );
    expect(getRankingReportLink({ reportId: 'AbC' }, 'retail')).toContain('www.warcraftlogs.com');
  });

  it('preserves player identity in analyzer links when rankings do not include source IDs', () => {
    const link = getRotationAnalyzerLink({
      reportId: 'ReportA',
      fights: [{ fightId: 3 }],
      players: [{ playerId: 0, name: 'Alice' }]
    });
    const params = new URLSearchParams(link!.slice(1));
    expect(params.get('playerName')).toBe('Alice');
    expect(params.has('sourceId')).toBe(false);
    expect(params.get('fightId')).toBe('3');
    expect(params.get('autoAnalyze')).toBe('1');
    expect(getRotationAnalyzerLink({ reportId: ' ' })).toBeNull();
  });
});
