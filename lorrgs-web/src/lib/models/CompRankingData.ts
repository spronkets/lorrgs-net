import type { RankingReport } from './RankingReport';

export type CompRankingData = {
  bossSlug: string;
  difficulty: string;
  updated: string;
  page: number;
  hasMorePages: boolean;
  totalCount: number;
  reports: RankingReport[];
};
