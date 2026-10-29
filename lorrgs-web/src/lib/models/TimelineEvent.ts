export type TimelineEvent = {
  spellId: number;
  timestamp: number;
  duration: number | null;
  kind: 'cast' | 'buff' | 'death' | 'marker';
  isInferred?: boolean;
};
