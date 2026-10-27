import type { RankingReport, ReportTimeline, TimelineEvent, TimelineSpell } from './api';

export const TIMELINE_ZOOM_LEVELS = [100, 200, 400, 800] as const;

export function timelineKey(report: RankingReport): string {
  const player = report.players?.[0];
  return `${report.reportId}:${report.fights?.[0]?.fightId ?? 0}:${player?.name ?? ''}:${player?.playerId ?? 0}`;
}

export function canLoadTimeline(report: RankingReport): boolean {
  return Boolean(
    report.fights?.[0]?.fightId > 0 &&
    (report.players?.[0]?.name || report.players?.[0]?.playerId > 0)
  );
}

export function timelineTime(milliseconds: number): string {
  const seconds = Math.floor(Math.max(0, milliseconds) / 1000);
  return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`;
}

export function castIntervals(events: TimelineEvent[], spellId: number) {
  const timestamps = [
    ...new Set(
      events
        .filter((event) => event.kind === 'cast' && event.spellId === spellId)
        .map((event) => event.timestamp)
    )
  ].sort((a, b) => a - b);
  return timestamps.slice(1).map((end, index) => ({
    start: timestamps[index],
    end,
    label: `${Number(((end - timestamps[index]) / 1000).toFixed(1))}s`
  }));
}

export function spellIconUrl(icon: string | null): string | null {
  // WCL icons are filenames, not arbitrary report-provided URLs.
  const filename = icon
    ?.split('/')
    .pop()
    ?.replace(/\.(jpg|png)$/i, '');
  return filename && /^[a-z0-9_-]+$/i.test(filename)
    ? `https://wow.zamimg.com/images/wow/icons/medium/${filename.toLowerCase()}.jpg`
    : null;
}

export function timelineSpells(timelines: ReportTimeline[]): TimelineSpell[] {
  const spells = new Map<number, TimelineSpell>();
  for (const timeline of timelines)
    for (const spell of timeline.spells) spells.set(spell.id, spell);
  return [...spells.values()].sort((a, b) => a.name.localeCompare(b.name));
}

// These are observed power windows, not estimates of their DPS contribution.
const powerWindowIds = new Set([2825, 32182, 80353, 90355, 160452, 264667, 390386, 10060, 31884]);
const powerWindowNames = new Set([
  'bloodlust',
  'heroism',
  'time warp',
  'ancient hysteria',
  'primal rage',
  'fury of the aspects',
  'power infusion',
  'avenging wrath',
  'haste',
  'drums of battle',
  'drums of war',
  'insane strength'
]);

export function isPowerWindow(spell: TimelineSpell | undefined): boolean {
  return Boolean(
    spell && (powerWindowIds.has(spell.id) || powerWindowNames.has(spell.name.trim().toLowerCase()))
  );
}

export function spellAppearance(spell: TimelineSpell | undefined, playerCast: boolean) {
  const group = isPowerWindow(spell) ? 'power' : playerCast ? 'ability' : 'received';
  const palettes = {
    power: ['#f4bd50', '#f7d976', '#e99a43', '#efc065'],
    ability: ['#69bafa', '#66d5df', '#9dacf9', '#77c5b0', '#b1b8ed'],
    received: ['#bba0ee', '#91d18b', '#e4a4ca', '#9ec8a5', '#c4b987']
  };
  return {
    group,
    label:
      group === 'power' ? 'DPS / power window' : playerCast ? 'Player ability' : 'Effect on player',
    color: palettes[group][Math.abs(spell?.id ?? 0) % palettes[group].length]
  };
}

export function timelineSpellGroups(timelines: ReportTimeline[]) {
  const castIds = new Set(
    timelines.flatMap((timeline) =>
      timeline.events.filter((event) => event.kind === 'cast').map((event) => event.spellId)
    )
  );
  const spells = timelineSpells(timelines);
  return [
    { label: 'DPS / power windows', spells: spells.filter(isPowerWindow) },
    {
      label: 'Player abilities',
      spells: spells.filter((spell) => !isPowerWindow(spell) && castIds.has(spell.id))
    },
    {
      label: 'Other effects on player',
      spells: spells.filter((spell) => !isPowerWindow(spell) && !castIds.has(spell.id))
    }
  ].filter((group) => group.spells.length);
}

export function visibleTimelineEvents(
  timeline: ReportTimeline,
  hidden: Set<number>,
  showCasts: boolean,
  showBuffs: boolean,
  showDeaths: boolean
): TimelineEvent[] {
  return timeline.events.filter((event) =>
    event.kind === 'death'
      ? showDeaths
      : !hidden.has(event.spellId) &&
        ((event.kind === 'cast' && showCasts) || (event.kind === 'buff' && showBuffs))
  );
}

export function eventLanes(events: TimelineEvent[]): { event: TimelineEvent; lane: number }[] {
  const lanes = new Map<number, number>();
  for (const id of [...new Set(events.map((event) => event.spellId))].sort((a, b) => a - b))
    lanes.set(id, lanes.size);
  return events.map((event) => ({ event, lane: lanes.get(event.spellId)! }));
}

export function timelineNote(timeline: ReportTimeline, hidden: Set<number>): string {
  const spells = new Map(timeline.spells.map((spell) => [spell.id, spell.name]));
  return [
    `${timeline.playerName} (${timelineTime(timeline.duration)})${timeline.isComplete ? '' : ' — INCOMPLETE'}`,
    ...timeline.events
      .filter((event) => event.kind === 'cast' && !hidden.has(event.spellId))
      .map(
        (event) => `${timelineTime(event.timestamp)} ${spells.get(event.spellId) ?? event.spellId}`
      )
  ].join('\n');
}

export function fightDuration(report: RankingReport, timeline?: ReportTimeline): number {
  return timeline?.duration || report.fights?.[0]?.duration || report.duration || 0;
}
