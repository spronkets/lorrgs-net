import type { RaidFightRanking } from './RaidFightRanking';
import type { TimelinePhase } from './TimelinePhase';
import type { CompRankingComposition } from './CompRankingComposition';

export type RankingFight = {
  speed?: RaidFightRanking | null;
  execution?: RaidFightRanking | null;
  composition?: CompRankingComposition;
  roleCounts?: Record<string, number>;
  deaths?: number;
  damageTaken?: number;
  encounterId?: number;
  phases?: TimelinePhase[] | null;
  fightId: number;
  name: string;
  duration: number;
  isKill: boolean;
  startTime: string;
  percentile: number | null;
};
