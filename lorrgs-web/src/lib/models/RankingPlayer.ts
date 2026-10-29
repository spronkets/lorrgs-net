import type { ReportTimeline } from './ReportTimeline';
import type { RankingCast } from './RankingCast';

export type RankingPlayer = {
  parsePercentile?: number | null;
  timeline?: ReportTimeline | null;
  roleCode?: string;
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
