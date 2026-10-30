import { describe, expect, it } from 'vitest';
import { manualRankingReport, parseReportCodes, readReportOverview } from './manualReports';

import { reportMetadata } from './testing/manualReportFixtures';

describe('manual report inputs', () => {
  it('reads report guild, faction and realm identity and passes it to pull summaries', () => {
    const report = readReportOverview({
      reportData: { report: {
        ...reportMetadata().reportData.report,
        guild: { name: 'Aftershock', faction: { id: 1 },
          server: { name: 'Thunderstrike', region: { slug: 'eu' } } }
      } }
    });
    expect(manualRankingReport(report, report.fights[0])).toMatchObject({
      guildName: 'Aftershock', factionId: 1, serverName: 'Thunderstrike', serverRegion: 'eu'
    });
  });
  it('accepts multiple codes and Warcraft Logs URLs and deduplicates', () => {
    expect(
      parseReportCodes('ReportA, https://fresh.warcraftlogs.com/reports/ReportB#fight=22;\nReportA')
    ).toEqual(['ReportA', 'ReportB']);
    expect(parseReportCodes('https://www.warcraftlogs.com/reports/ReportC/')).toEqual(['ReportC']);
  });
  it.each([
    '',
    'https://evil.test/reports/ReportA',
    'https://warcraftlogs.com/characters/a',
    'https://',
    'bad-code',
    'a'.repeat(33)
  ])('rejects invalid input %s', (value) => {
    expect(() => parseReportCodes(value)).toThrow();
  });
  it('reads boss pulls and creates timed summary reports without inventing rankings', () => {
    const report = readReportOverview(reportMetadata());
    expect(report.fights).toHaveLength(3);
    expect(report.zone?.name).toBe("Zul'Aman");
    const summary = manualRankingReport(report, report.fights[1]);
    expect(summary).toMatchObject({
      reportId: 'ReportA',
      duration: 40000,
      percentile: null,
      fights: [{ fightId: 2, isKill: false, encounterId: 10 }]
    });
    expect(summary.startTime).toBe(new Date(report.startTime + 110000).toISOString());
  });
  it.each([
    null,
    {},
    { reportData: null },
    { reportData: { report: null } },
    { reportData: { report: {} } },
    { reportData: { report: { ...reportMetadata().reportData.report, fights: [{}] } } }
  ])('rejects missing or malformed metadata', (value) =>
    expect(() => readReportOverview(value)).toThrow()
  );
  it('supports reports without a zone', () => {
    expect(
      readReportOverview({
        reportData: {
          report: {
            ...reportMetadata().reportData.report,
            zone: null
          }
        }
      }).zone
    ).toBeNull();
  });
  it('ignores trash fights with null boss metadata but still rejects incomplete boss pulls', () => {
    const payload = reportMetadata();
    expect(readReportOverview(payload).fights.map((fight) => fight.id)).toEqual([1, 2, 3]);
    expect(() =>
      readReportOverview({
        reportData: {
          report: {
            ...payload.reportData.report,
            fights: [{ ...payload.reportData.report.fights[0], kill: null }]
          }
        }
      })
    ).toThrow('Warcraft Logs returned incomplete fight metadata.');
  });
});
