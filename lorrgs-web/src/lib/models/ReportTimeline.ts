import type { TimelineSpell } from './TimelineSpell';
import type { TimelineEvent } from './TimelineEvent';

export type ReportTimeline = {
  reportCode: string;
  fightId: number;
  sourceId: number;
  playerName: string;
  duration: number;
  isComplete: boolean;
  spells: TimelineSpell[];
  events: TimelineEvent[];
};
