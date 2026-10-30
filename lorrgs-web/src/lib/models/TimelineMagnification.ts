import type { ReportTimeline } from './ReportTimeline';
import type { TimelineEvent } from './TimelineEvent';

export type TimelineMagnification = {
  timeline: ReportTimeline;
  start: number;
  span: number;
  zoom: number;
  x: number;
  y: number;
  trackTop?: number;
  events?: TimelineEvent[];
  hoveredEvent?: TimelineEvent;
  mergedResources?: boolean;
  showIcons?: boolean;
  previewPixelsPerMillisecond?: number;
  timestamp?: number;
  resourceMode?: 'health' | 'mana';
  boss?: boolean;
  phases?: { id: number; timestamp: number }[];
};
