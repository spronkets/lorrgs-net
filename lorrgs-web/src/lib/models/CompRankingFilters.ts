export type CompRankingFilters = {
  roles: string[];
  specs: string[];
  minSeconds: number;
  maxSeconds: number;
  region: string;
  fromDate: string;
  toDate: string;
  sort: 'rank' | 'performance' | 'duration' | 'date';
};
