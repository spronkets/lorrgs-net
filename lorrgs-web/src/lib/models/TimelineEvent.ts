export type TimelineEvent = {
  spellId: number;
  timestamp: number;
  duration: number | null;
  kind:
    | 'cast'
    | 'buff'
    | 'death'
    | 'marker'
    | 'heal'
    | 'hot'
    | 'damage'
    | 'mana-spent'
    | 'mana-returned';
  isInferred?: boolean;
  amount?: number | null;
  castStart?: number | null;
  sourceName?: string | null;
  healthBeforePercent?: number | null;
  healthAfterPercent?: number | null;
  healthBeforeIsPreviousSnapshot?: boolean;
  displayBelow?: boolean;
  isManaDrain?: boolean;
  castResults?: {
    targetId: number;
    targetName: string;
    kind: 'heal' | 'damage' | 'mana-returned';
    amount: number;
    outcome?: string | null;
  }[] | null;
};
