export type TimelinePhase = {
  id: number;
  timestamp: number;
  isIntermission?: boolean;
  notePhaseId?: number | null;
};
