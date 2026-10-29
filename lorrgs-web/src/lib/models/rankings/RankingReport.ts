import type { RankingPlayer } from './RankingPlayer';

export type RankingReport = {
  percentile?: number | null;
  players?: RankingPlayer[];
  duration?: number | null;
  startTime?: string | null;
};
