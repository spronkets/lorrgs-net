import type { ReportTimeline } from './api';

export type InspectedSpell = { id: number; name: string; detail: string; color: string };
export type TimelineMagnification = {
  timeline: ReportTimeline;
  start: number;
  span: number;
  zoom: number;
  x: number;
  y: number;
};
