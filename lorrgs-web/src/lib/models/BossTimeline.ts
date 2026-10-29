import type { TimelineSpell } from './TimelineSpell';
import type { TimelineEvent } from './TimelineEvent';
import type { TimelinePhase } from './TimelinePhase';

export type BossTimeline = {
  reportCode: string;
  fightId: number;
  name: string;
  duration: number;
  isComplete: boolean;
  spells: TimelineSpell[];
  events: TimelineEvent[];
  phases: TimelinePhase[];
};
