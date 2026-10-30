import { get, send } from './common/http/apiClient';

import type { RankingReport } from './models/RankingReport';
import type { SpecRankingData } from './models/SpecRankingData';
import type { ReportTimeline } from './models/ReportTimeline';
import type { BossTimeline } from './models/BossTimeline';
import type { SpecRankingQueryOptions } from './models/SpecRankingQueryOptions';
import type { RaidCatalog } from './models/RaidCatalog';
import type { CompRankingData } from './models/CompRankingData';
import type { CompRankingQuery } from './models/CompRankingQuery';

export function getBossTimeline(
  report: RankingReport,
  edition: string,
  signal?: AbortSignal
): Promise<BossTimeline> {
  const params = new URLSearchParams({
    fightId: String(report.fights?.[0]?.fightId ?? 0),
    edition
  });
  return get<BossTimeline>(
    `/warcraftlogs/reports/${encodeURIComponent(report.reportId)}/timeline/boss?${params}`,
    { signal }
  );
}

function timelineEndpoint(report: RankingReport, edition: string): string {
  const player = report.players?.[0];
  const params = new URLSearchParams({
    fightId: String(report.fights?.[0]?.fightId ?? 0),
    edition
  });
  if (player?.playerId && player.playerId > 0) params.set('sourceId', String(player.playerId));
  if (player?.name) params.set('playerName', player.name);
  if (player?.specId && player.specId > 0) params.set('specId', String(player.specId));
  return `/warcraftlogs/reports/${encodeURIComponent(report.reportId)}/timeline?${params}`;
}

export async function getReportTimeline(
  report: RankingReport,
  edition: string,
  signal?: AbortSignal
): Promise<ReportTimeline> {
  const endpoint = timelineEndpoint(report, edition);
  if (signal?.aborted) throw new DOMException('Aborted', 'AbortError');
  return get<ReportTimeline>(endpoint, { signal });
}

function appendSpecRankingQueryOptions(
  params: URLSearchParams,
  options: SpecRankingQueryOptions = {}
): void {
  if (typeof options.zoneId === 'number') {
    params.set('zoneId', String(options.zoneId));
  }

  if (typeof options.encounterId === 'number') {
    params.set('encounterId', String(options.encounterId));
  }

  if (options.className) {
    params.set('className', options.className);
  }

  if (options.specName) {
    params.set('specName', options.specName);
  }

  if (typeof options.difficultyId === 'number') {
    params.set('difficultyId', String(options.difficultyId));
  }

  if (typeof options.partition === 'number') {
    params.set('partition', String(options.partition));
  }

  if (typeof options.bracket === 'number') {
    params.set('bracket', String(options.bracket));
  }

  if (options.serverRegion) {
    params.set('serverRegion', options.serverRegion);
  }

  if (options.serverSlug) {
    params.set('serverSlug', options.serverSlug);
  }

  if (options.filter) {
    params.set('filter', options.filter);
  }

  if (options.hardModeLevel) {
    params.set('hardModeLevel', options.hardModeLevel);
  }

  if (options.externalBuffs) {
    params.set('externalBuffs', options.externalBuffs);
  }

  if (typeof options.includeCombatantInfo === 'boolean') {
    params.set('includeCombatantInfo', String(options.includeCombatantInfo));
  }

  if (typeof options.includeOtherPlayers === 'boolean') {
    params.set('includeOtherPlayers', String(options.includeOtherPlayers));
  }
}

export async function getRaidCatalog(): Promise<RaidCatalog> {
  try {
    const payload = await get('/raid-catalog');
    return (
      payload && typeof payload === 'object' ? payload : { editions: [], instances: {} }
    ) as RaidCatalog;
  } catch {
    return { editions: [], instances: {} };
  }
}

// Rankings endpoints
export async function getSpecRankings(
  specSlug: string,
  bossSlug: string,
  difficulty = 'Mythic',
  metric = 'dps',
  edition = '',
  options: SpecRankingQueryOptions = {}
): Promise<SpecRankingData> {
  const normalizedDifficulty = String(difficulty || 'mythic')
    .trim()
    .toLowerCase();
  const normalizedMetric = String(metric || 'dps')
    .trim()
    .toLowerCase();
  const params = new URLSearchParams({
    difficulty: normalizedDifficulty,
    metric: normalizedMetric
  });
  if (edition) {
    params.set('edition', edition);
  }

  appendSpecRankingQueryOptions(params, options);

  return get<SpecRankingData>(`/rankings/spec/${specSlug}/${bossSlug}?${params}`);
}

export async function getSpecRankingsInfo(
  specSlug: string,
  bossSlug: string,
  edition = '',
  options: SpecRankingQueryOptions = {}
): Promise<SpecRankingData> {
  const params = new URLSearchParams();
  if (edition) {
    params.set('edition', edition);
  }
  appendSpecRankingQueryOptions(params, options);
  const suffix = params.size ? `?${params}` : '';
  return get<SpecRankingData>(`/rankings/spec/${specSlug}/${bossSlug}/info${suffix}`);
}

export async function getCompRankings(
  bossSlug: string,
  roles: string[] = [],
  specs: string[] = [],
  edition = '',
  options?: CompRankingQuery,
  signal?: AbortSignal
) {
  const params = new URLSearchParams();
  if (roles.length) roles.forEach((r) => params.append('role', r));
  if (specs.length) specs.forEach((s) => params.append('spec', s));
  if (edition) params.set('edition', edition);
  if (options) {
    params.set('encounterId', String(options.encounterId));
    if (options.zoneId) params.set('zoneId', String(options.zoneId));
    params.set('difficultyId', String(options.difficultyId));
    params.set('page', String(options.page ?? 1));
    params.set('killtime_min', String(options.minSeconds ?? 0));
    params.set('killtime_max', String(options.maxSeconds ?? 0));
    if (options.refresh) params.set('refresh', 'true');
  }
  return get<CompRankingData>(`/rankings/comp/${encodeURIComponent(bossSlug)}?${params}`, {
    signal
  });
}

export function getCompRankingDetails(
  report: RankingReport,
  edition: string,
  refresh = false,
  signal?: AbortSignal
): Promise<RankingReport> {
  const params = new URLSearchParams({ edition });
  if (refresh) params.set('refresh', 'true');
  return get<RankingReport>(
    `/rankings/comp/reports/${encodeURIComponent(report.reportId)}/fights/${report.fights?.[0]?.fightId ?? 0}?${params}`,
    { signal }
  );
}

export async function queueSpecRankingUpdate(
  specSlug: string,
  bossSlug: string,
  difficulty: string,
  metric: string,
  limit = 25
) {
  const params = new URLSearchParams({
    specSlug,
    bossSlug,
    difficulty,
    metric,
    limit: String(limit)
  });
  return get(`/rankings/spec/queue?${params}`, { method: 'POST' });
}

export async function queueCompRankingUpdate(bossSlug: string, limit = 25) {
  const params = new URLSearchParams({ bossSlug, limit: String(limit) });
  return get(`/rankings/comp/queue?${params}`, { method: 'POST' });
}

export async function markRankingDirty(
  specSlug: string,
  bossSlug: string,
  difficulty: string,
  metric: string
) {
  const params = new URLSearchParams({ specSlug, bossSlug, difficulty, metric });
  return get(`/rankings/spec/dirty?${params}`, { method: 'PATCH' });
}

// Rotation analysis endpoints
export async function lookupReport(reportCode: string, signal?: AbortSignal) {
  return get(`/warcraftlogs/reports/${encodeURIComponent(reportCode)}/lookup`, { signal });
}

export async function analyzeRotation(payload: unknown) {
  return send('/warcraftlogs/rotation/analyze', payload, { method: 'POST' });
}
