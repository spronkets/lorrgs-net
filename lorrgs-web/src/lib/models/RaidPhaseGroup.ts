import type { RaidInstance } from './RaidInstance';

export type RaidPhaseGroup = {
  key: string;
  label: string;
  phase: number | null;
  raids: RaidInstance[];
};
