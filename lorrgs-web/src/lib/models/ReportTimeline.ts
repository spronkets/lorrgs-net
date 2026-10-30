import type { TimelineSpell } from './TimelineSpell';
import type { TimelineEvent } from './TimelineEvent';

export type ReportTimeline = {
  maxHealth?: number | null;
  estimatedMaxHealth?: number | null;
  healthCapacities?: {
    timestamp: number;
    maxHealth: number | null;
    estimatedMaxHealth: number | null;
  }[];
  maxMana?: number | null;
  reportCode: string;
  fightId: number;
  sourceId: number;
  playerName: string;
  duration: number;
  isComplete: boolean;
  spells: TimelineSpell[];
  events: TimelineEvent[];
};
