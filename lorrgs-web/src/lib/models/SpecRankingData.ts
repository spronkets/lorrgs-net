import type { RankingReport } from './RankingReport';

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
