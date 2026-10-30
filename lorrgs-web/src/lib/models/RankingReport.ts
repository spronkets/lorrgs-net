import type { RankingFight } from './RankingFight';
import type { RankingPlayer } from './RankingPlayer';
import type { ReportTimeline } from './ReportTimeline';
import type { BossTimeline } from './BossTimeline';

export type RankingReport = {
  guildName?: string | null;
  serverName?: string | null;
  serverRegion?: string | null;
  factionId?: number | null;
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
