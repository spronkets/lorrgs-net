export type RankingPlayer = {
  performance?: number | null;
};

export type RankingReport = {
  percentile?: number | null;
  players?: RankingPlayer[];
  duration?: number | null;
};

export function getDisplayPercentile(value: unknown): string {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return `${value.toFixed(1)}%`;
  }

  return 'N/A';
}
