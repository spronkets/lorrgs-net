import type { RankingReport } from './models/RankingReport';
import type { RankingFight } from './models/RankingFight';

export const MANUAL_REPORT_QUERY_KEYS = ['report', 'reportBoss', 'pull', 'loadedPull'] as const;

export type ReportOverview = {
  code: string;
  title: string;
  startTime: number;
  guildName?: string;
  serverName?: string;
  serverRegion?: string;
  factionId?: number;
  zone: { id: number; name: string } | null;
  fights: {
    id: number;
    name: string;
    kill: boolean;
    difficulty: number;
    encounterID: number;
    startTime: number;
    endTime: number;
  }[];
};

export function parseReportCodes(input: string): string[] {
  const entries = input
    .trim()
    .split(/[\s,;]+/)
    .filter(Boolean);
  if (!entries.length) throw new Error('Enter at least one report code or Warcraft Logs URL.');
  return [
    ...new Set(
      entries.map((entry) => {
        let code = entry;
        if (/^https?:/i.test(entry)) {
          let url: URL;
          try {
            url = new URL(entry);
          } catch {
            throw new Error(`Invalid report URL: ${entry}`);
          }
          if (!/^(?:[a-z0-9-]+\.)?warcraftlogs\.com$/i.test(url.hostname))
            throw new Error('Report URLs must be from warcraftlogs.com.');
          code = /^\/reports\/([a-z0-9]+)(?:\/|$)/i.exec(url.pathname)?.[1] ?? '';
        }
        if (!/^[a-z0-9]{1,32}$/i.test(code))
          throw new Error('Report codes must contain 1–32 letters or numbers.');
        return code;
      })
    )
  ];
}

export function readReportOverview(payload: unknown): ReportOverview {
  if (!payload || typeof payload !== 'object' || !('reportData' in payload))
    throw new Error('Warcraft Logs returned invalid report metadata.');
  const data = payload.reportData;
  if (
    !data ||
    typeof data !== 'object' ||
    !('report' in data) ||
    !data.report ||
    typeof data.report !== 'object'
  )
    throw new Error('Report not found or unavailable.');
  const report = data.report;
  if (
    !('code' in report) ||
    typeof report.code !== 'string' ||
    !('title' in report) ||
    typeof report.title !== 'string' ||
    !('startTime' in report) ||
    typeof report.startTime !== 'number' ||
    !('fights' in report) ||
    !Array.isArray(report.fights)
  )
    throw new Error('Warcraft Logs returned incomplete report metadata.');
  const fights: ReportOverview['fights'] = [];
  for (const fight of report.fights) {
    if (!fight || typeof fight !== 'object' || typeof fight.encounterID !== 'number')
      throw new Error('Warcraft Logs returned incomplete fight metadata.');
    if (fight.encounterID <= 0) continue;
    if (
      typeof fight.id !== 'number' ||
      typeof fight.name !== 'string' ||
      typeof fight.kill !== 'boolean' ||
      typeof fight.startTime !== 'number' ||
      typeof fight.endTime !== 'number' ||
      typeof fight.difficulty !== 'number'
    )
      throw new Error('Warcraft Logs returned incomplete fight metadata.');
    if (fight.endTime > fight.startTime) fights.push(fight);
  }
  const zone =
    'zone' in report &&
    report.zone &&
    typeof report.zone === 'object' &&
    'id' in report.zone &&
    typeof report.zone.id === 'number' &&
    'name' in report.zone &&
    typeof report.zone.name === 'string'
      ? { id: report.zone.id, name: report.zone.name }
      : null;
  const guild = 'guild' in report && isObject(report.guild) ? report.guild : undefined;
  const server = guild && isObject(guild.server) ? guild.server : undefined;
  const region = server && isObject(server.region) ? server.region : undefined;
  const faction = guild && isObject(guild.faction) ? guild.faction : undefined;
  return {
    code: report.code, title: report.title, startTime: report.startTime, zone, fights,
    guildName: typeof guild?.name === 'string' ? guild.name : undefined,
    serverName: typeof server?.name === 'string' ? server.name : undefined,
    serverRegion: typeof region?.slug === 'string' ? region.slug : undefined,
    factionId: typeof faction?.id === 'number' ? faction.id : undefined
  };
}

function isObject(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === 'object';
}

export function manualRankingReport(
  report: ReportOverview,
  fight: ReportOverview['fights'][number]
): RankingReport & { fights: [RankingFight] } {
  const duration = fight.endTime - fight.startTime;
  const startTime = new Date(report.startTime + fight.startTime).toISOString();
  return {
    reportId: report.code,
    guildName: report.guildName,
    serverName: report.serverName,
    serverRegion: report.serverRegion,
    factionId: report.factionId,
    title: report.title,
    duration,
    startTime,
    percentile: null,
    players: [],
    fights: [
      {
        fightId: fight.id,
        name: fight.name,
        duration,
        startTime,
        isKill: fight.kill,
        encounterId: fight.encounterID,
        percentile: null
      }
    ]
  };
}
