export type CompRankingQuery = {
  encounterId: number;
  difficultyId: number;
  page?: number;
  minSeconds?: number;
  maxSeconds?: number;
  refresh?: boolean;
};
