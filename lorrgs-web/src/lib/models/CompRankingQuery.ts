export type CompRankingQuery = {
  encounterId: number;
  zoneId?: number;
  difficultyId: number;
  page?: number;
  minSeconds?: number;
  maxSeconds?: number;
  refresh?: boolean;
};
