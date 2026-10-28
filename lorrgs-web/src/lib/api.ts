import { RATE_LIMIT_MESSAGE } from './timelineError';

const API_BASE: string =
  (typeof import.meta !== 'undefined' && import.meta.env?.VITE_API_BASE) || '/api';
const HEALTH_READY_URL: string | undefined =
  typeof import.meta !== 'undefined' ? import.meta.env?.VITE_HEALTH_URL : undefined;
const API_TIMEOUT_MS = 30000;

type ApiOptions = {
  method?: string;
  headers?: HeadersInit;
  signal?: AbortSignal;
};

export type WowRole = {
  id: number;
  name: string;
  nameSlug: string;
  code: string;
  metric?: string;
  Metric?: string;
  color: string;
};

export type WowClass = {
  id: number;
  name: string;
  nameSlug: string;
  color: string;
  maskId: number;
};

export type WowSpec = {
  id: number;
  name: string;
  nameSlug: string;
  fullName: string;
  fullNameSlug: string;
  classId: number;
  roleId: number;
  wowClass?: WowClass | null;
  role?: WowRole | null;
};

export type WorldDataSnapshot = {
  roles?: WowRole[];
  classes?: WowClass[];
  specs?: WowSpec[];
  bosses?: { id: number; nameSlug: string; icon: string; raidId: number }[];
  zones?: { id: number; slug: string; icon: string }[];
};

export type RankingCast = {
  spellId: number;
  spellName: string;
  count: number;
  totalTime: number;
  totalDamage: number;
  percentage: number;
};

export type RankingPlayer = {
  playerId: number;
  name: string;
  guildName?: string | null;
  factionId?: number | null;
  faction?: string | null;
  guildFaction?: number | null;
  serverRegion?: string | null;
  serverName?: string | null;
  serverSlug?: string | null;
  classId: number;
  specId: number;
  specSlug: string;
  performance: number;
  casts?: RankingCast[];
};

export type RankingFight = {
  encounterId?: number;
  phases?: TimelinePhase[] | null;
  fightId: number;
  name: string;
  duration: number;
  isKill: boolean;
  startTime: string;
  percentile: number | null;
};

export type RankingReport = {
  reportId: string;
  title: string;
  percentile: number | null;
  duration: number;
  durationDisplay?: string;
  startTime: string;
  fights?: RankingFight[];
  players?: RankingPlayer[];
  timeline?: ReportTimeline | null;
  bossTimeline?: BossTimeline | null;
};

export type SpecRankingData = {
  bossIconUrl?: string | null;
  specSlug: string;
  bossSlug: string;
  difficulty: string;
  metric: string;
  page?: number;
  hasMorePages?: boolean;
  totalCount?: number;
  reports: RankingReport[];
  updated: string;
  isDirty: boolean;
  percentile: number | null;
};

export type TimelineSpell = { id: number; name: string; icon: string | null };
export type TimelineEvent = {
  spellId: number;
  timestamp: number;
  duration: number | null;
  kind: 'cast' | 'buff' | 'death' | 'marker';
  isInferred?: boolean;
};
export type TimelinePhase = {
  id: number;
  timestamp: number;
  isIntermission?: boolean;
  notePhaseId?: number | null;
};
export type ReportTimeline = {
  reportCode: string;
  fightId: number;
  sourceId: number;
  playerName: string;
  duration: number;
  isComplete: boolean;
  spells: TimelineSpell[];
  events: TimelineEvent[];
};
export type BossTimeline = {
  reportCode: string;
  fightId: number;
  name: string;
  duration: number;
  isComplete: boolean;
  spells: TimelineSpell[];
  events: TimelineEvent[];
  phases: TimelinePhase[];
};

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

export type SpecRankingQueryOptions = {
  zoneId?: number;
  encounterId?: number;
  className?: string;
  specName?: string;
  difficultyId?: number;
  partition?: number;
  bracket?: number;
  serverRegion?: string;
  serverSlug?: string;
  filter?: string;
  hardModeLevel?: string;
  externalBuffs?: string;
  includeCombatantInfo?: boolean;
  includeOtherPlayers?: boolean;
};

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

export function buildApiUrl(endpoint: string): string {
  return `${API_BASE}${endpoint}`;
}

function deriveHealthUrls(): string[] {
  if (HEALTH_READY_URL) {
    return [HEALTH_READY_URL];
  }

  const trimmedBase = API_BASE.replace(/\/+$/, '');
  const defaultUrls = ['/health'];

  if (/^https?:\/\//i.test(trimmedBase)) {
    const baseWithoutApi = trimmedBase.replace(/\/api$/i, '');
    return [`${baseWithoutApi}/health`];
  }

  return defaultUrls;
}

async function probeHealth(url: string): Promise<boolean> {
  try {
    const response = await fetch(url, {
      method: 'GET',
      cache: 'no-store'
    });

    return response.ok;
  } catch {
    return false;
  }
}

export async function waitForHealthReady(timeoutMs = 30000, intervalMs = 500): Promise<boolean> {
  const urls = deriveHealthUrls();
  const deadline = Date.now() + timeoutMs;

  while (Date.now() < deadline) {
    for (const url of urls) {
      if (await probeHealth(url)) {
        return true;
      }
    }

    await new Promise((resolve) => setTimeout(resolve, intervalMs));
  }

  return false;
}

export function normalizeApiPayload(payload: unknown): unknown[] {
  if (Array.isArray(payload)) {
    return payload;
  }

  if (payload && typeof payload === 'object') {
    return Object.values(payload as Record<string, unknown>);
  }

  return [];
}

export function normalizeApiCollection(payload: unknown, fallbackKey = ''): unknown[] {
  if (Array.isArray(payload)) {
    return payload;
  }

  if (payload && typeof payload === 'object') {
    const record = payload as Record<string, unknown>;

    if (fallbackKey && Array.isArray(record[fallbackKey])) {
      return record[fallbackKey] as unknown[];
    }

    if (Object.values(record).every((value) => value && typeof value === 'object')) {
      return Object.values(record);
    }
  }

  return [];
}

async function buildApiError(response: Response): Promise<Error> {
  if (response.status === 429) return new Error(RATE_LIMIT_MESSAGE);
  let detail = '';

  try {
    const contentType = response.headers.get('content-type') || '';

    if (
      contentType.includes('application/json') ||
      contentType.includes('application/problem+json')
    ) {
      const payload = (await response.json()) as Record<string, unknown>;
      if (typeof payload.error === 'string' && payload.error.trim()) {
        detail = payload.error.trim();
      } else if (typeof payload.message === 'string' && payload.message.trim()) {
        detail = payload.message.trim();
      } else if (typeof payload.detail === 'string' && payload.detail.trim()) {
        detail = payload.detail.trim();
      }
    } else {
      const text = (await response.text()).trim();
      if (text) {
        detail = text;
      }
    }
  } catch {
    // Fallback to status text below.
  }

  const statusSummary = `API Error: ${response.status} ${response.statusText}`.trim();
  return new Error(detail ? `${statusSummary} - ${detail}` : statusSummary);
}

export async function get<T = unknown>(endpoint: string, options: ApiOptions = {}): Promise<T> {
  const url = buildApiUrl(endpoint);
  const controller = new AbortController();
  const cancel = () => controller.abort();
  options.signal?.addEventListener('abort', cancel, { once: true });
  if (options.signal?.aborted) controller.abort();
  const timeoutHandle = setTimeout(() => controller.abort(), API_TIMEOUT_MS);

  let response: Response;
  try {
    response = await fetch(url, {
      method: 'GET',
      headers: { 'Content-Type': 'application/json', ...options.headers },
      ...options,
      signal: controller.signal
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      if (options.signal?.aborted) throw error;
      throw new Error(`API Error: request timed out after ${API_TIMEOUT_MS}ms`);
    }

    throw error;
  } finally {
    clearTimeout(timeoutHandle);
    options.signal?.removeEventListener('abort', cancel);
  }

  if (!response.ok) {
    throw await buildApiError(response);
  }

  return response.json() as Promise<T>;
}

export async function send(
  endpoint: string,
  body: unknown,
  options: ApiOptions = {}
): Promise<unknown> {
  const url = buildApiUrl(endpoint);
  const controller = new AbortController();
  const timeoutHandle = setTimeout(() => controller.abort(), API_TIMEOUT_MS);

  let response: Response;
  try {
    response = await fetch(url, {
      method: options.method || 'POST',
      headers: { 'Content-Type': 'application/json', ...options.headers },
      body: JSON.stringify(body ?? {}),
      signal: controller.signal,
      ...options
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw new Error(`API Error: request timed out after ${API_TIMEOUT_MS}ms`);
    }

    throw error;
  } finally {
    clearTimeout(timeoutHandle);
  }

  if (!response.ok) {
    throw await buildApiError(response);
  }

  return response.json();
}

// World Data endpoints
export async function getSeasons() {
  try {
    const payload = await get('/worlddata/seasons');
    return normalizeApiCollection(payload, 'seasons');
  } catch {
    return [];
  }
}

export async function getSeason(slug: string) {
  return get(`/worlddata/seasons/${slug}`);
}

export async function getClasses() {
  const payload = await get('/worlddata/classes');
  return normalizeApiCollection(payload, 'classes');
}

export async function getSpecs() {
  const payload = await get('/worlddata/specs');
  return normalizeApiCollection(payload, 'specs');
}

export async function getRoles() {
  const payload = await get('/worlddata/roles');
  return normalizeApiCollection(payload, 'roles');
}

export async function getZones() {
  const payload = await get('/worlddata/zones');
  return normalizeApiCollection(payload, 'zones');
}

export async function getZone(slug: string) {
  return get(`/worlddata/zones/${slug}`);
}

export async function getZoneBosses(zoneSlug: string) {
  const payload = await get(`/worlddata/zones/${zoneSlug}/bosses`);
  return normalizeApiCollection(payload, 'bosses');
}

export async function getBosses() {
  const payload = await get('/worlddata/bosses');
  return normalizeApiCollection(payload, 'bosses');
}

export async function getBoss(slug: string) {
  return get(`/worlddata/bosses/${slug}`);
}

export async function getSpells() {
  const payload = await get('/worlddata/spells');
  return normalizeApiCollection(payload, 'spells');
}

export async function getSpell(id: string | number) {
  return get(`/worlddata/spells/${id}`);
}

export async function getTrinkets() {
  const payload = await get('/worlddata/trinkets');
  return normalizeApiCollection(payload, 'trinkets');
}

export async function getTrinket(id: string | number) {
  return get(`/worlddata/trinkets/${id}`);
}

type RaidCatalog = {
  editions: unknown[];
  instances: Record<string, unknown>;
};

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
  limit = 25,
  roles: string[] = [],
  specs: string[] = [],
  edition = ''
) {
  const params = new URLSearchParams({ limit: String(limit) });
  if (roles.length) roles.forEach((r) => params.append('role', r));
  if (specs.length) specs.forEach((s) => params.append('spec', s));
  if (edition) params.set('edition', edition);
  return get(`/rankings/comp/${bossSlug}?${params}`);
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
export async function lookupReport(reportCode: string) {
  return get(`/warcraftlogs/reports/${reportCode}/lookup`);
}

export async function analyzeRotation(payload: unknown) {
  return send('/warcraftlogs/rotation/analyze', payload, { method: 'POST' });
}
