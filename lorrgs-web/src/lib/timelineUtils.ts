import type { RankingReport } from './models/RankingReport';
import type { ReportTimeline } from './models/ReportTimeline';
import type { TimelineEvent } from './models/TimelineEvent';
import type { TimelineSpell } from './models/TimelineSpell';

export const TIMELINE_ZOOM_LEVELS = [100, 200, 400, 800] as const;

export function isResourceHit(event: TimelineEvent): boolean {
  return (
    event.kind === 'heal' ||
    event.kind === 'hot' ||
    event.kind === 'damage' ||
    event.kind === 'mana-spent' ||
    event.kind === 'mana-returned'
  );
}

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

export function preciseTimelineTime(milliseconds: number): string {
  const value = Math.max(0, Math.round(milliseconds / 10) * 10);
  const fraction = String((value % 1000) / 10)
    .padStart(2, '0')
    .replace(/0+$/, '');
  return `${timelineTime(value)}${fraction ? `.${fraction}` : ''}`;
}

export function timelineEventDetail(
  event: TimelineEvent,
  spell: TimelineSpell | undefined
): string {
  return timelineEventDetails(event, spell).join(' · ');
}

export function timelineEventDetails(
  event: TimelineEvent,
  spell: TimelineSpell | undefined,
  includeTiming = true
): string[] {
  if (event.kind === 'heal' || event.kind === 'hot' || event.kind === 'damage') {
    const percent = (value: number) => Number(value.toFixed(2));
    const delta =
      event.healthBeforePercent != null && event.healthAfterPercent != null
        ? percent(event.healthAfterPercent - event.healthBeforePercent)
        : null;
    return [
      preciseTimelineTime(event.timestamp),
      spell?.name ?? `Spell ${event.spellId}`,
      ...(event.amount != null
        ? [
            `${event.kind === 'damage' ? '-' : '+'}${event.amount.toLocaleString(undefined, { maximumFractionDigits: 2 })}`
          ]
        : []),
      `(${event.healthAfterPercent != null ? `${percent(event.healthAfterPercent)}%` : 'unknown'}, ${delta != null ? `${delta >= 0 ? '+' : ''}${delta}%` : 'unknown change'}${event.sourceName ? `, by ${event.sourceName}` : ''})`
    ];
  }
  const labels = {
    cast: 'Cast',
    buff: 'Buff',
    death: 'Death',
    marker: 'Inferred marker',
    heal: 'Direct heal received',
    hot: 'HoT tick received',
    damage: 'Damage taken',
    'mana-spent': 'Mana spent',
    'mana-returned': 'Mana returned'
  };
  return [
    preciseTimelineTime(event.timestamp),
    spell?.name ?? (event.kind === 'death' ? 'Death' : `Spell ${event.spellId}`),
    ...(event.kind === 'cast' && spell?.name.trim().toLowerCase() === 'melee'
      ? []
      : [labels[event.kind]]),
    ...(event.amount != null
      ? [event.amount.toLocaleString(undefined, { maximumFractionDigits: 2 })]
      : []),
    ...(event.sourceName ? [`by ${event.sourceName}`] : []),
    ...(includeTiming && event.castStart != null
      ? [`Cast started ${preciseTimelineTime(event.castStart)}`]
      : []),
    ...(includeTiming && event.duration != null
      ? [
          `${preciseTimelineTime(event.duration)} ${event.isInferred && event.kind === 'buff' ? 'displayed uptime' : `${event.isInferred ? 'inferred' : 'measured'} ${event.kind === 'cast' ? 'cast time' : 'uptime'}`}`
        ]
      : []),
    ...(isPowerWindow(spell) ? ['DPS / power window'] : []),
    ...(event.isInferred && event.kind !== 'buff' ? ['inferred'] : [])
  ];
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

export function isChanneledSpell(spell: TimelineSpell | undefined): boolean {
  return (
    spell != null &&
    /^(evocation|tranquility|hurricane|blizzard|arcane missiles|mind flay|mind sear|drain life|drain mana|drain soul|health funnel|hellfire|rain of fire|penance|divine hymn|hymn of hope|starshards|volley|first aid|.* bandage)( \(rank \d+\))?$/i.test(
      spell.name
    )
  );
}

export const healingSpellNames =
  /^(holy light|flash of light|holy shock|lay on hands|light of dawn|word of glory|renew|rejuvenation|regrowth|healing touch|chain heal|healing wave|lesser healing wave|prayer of healing|binding heal|flash heal|greater heal)(?: \(rank \d+\))?$/i;

export function resourceEventColor(
  event: TimelineEvent,
  spell: TimelineSpell | undefined
): string | undefined {
  if (event.kind === 'mana-spent') return '#d9ad69';
  if (event.kind === 'mana-returned') return '#69aaff';
  if (event.kind === 'damage') return '#ff6969';
  if (event.kind === 'heal' || event.kind === 'hot') return '#69d98b';
  if (event.kind === 'cast') {
    if (
      event.castResults?.some((result) => result.kind === 'heal') ||
      (spell && healingSpellNames.test(spell.name))
    )
      return '#69d98b';
    if (event.castResults?.some((result) => result.kind === 'damage')) return '#ff6969';
  }
  if (
    spell &&
    /\b(healing potion|rejuvenation potion|healthstone|healing stone|ironshield|stoneshield|armor potion|healing injector)\b/i.test(
      spell.name
    )
  )
    return '#69d98b';
  if (
    spell &&
    /\b(mana potion|mana injector|mana gem|dark rune|demonic rune|innervate|evocation|mana tide|divine plea|shadowfiend|hymn of hope|life tap|arcane torrent)\b/i.test(
      spell.name
    )
  )
    return '#69aaff';
  if (event.displayBelow) {
    return '#69d98b';
  }
  if (spell && /potion|healthstone|injector|mana gem|dark rune|demonic rune/i.test(spell.name))
    return '#69aaff';
  return undefined;
}

export type TimelineVisibilityOptions = {
  showCasts: boolean;
  showBuffs: boolean;
  showDeaths: boolean;
  showDirectHeals?: boolean;
  showHots?: boolean;
  showDamageTaken?: boolean;
  showManaSpent?: boolean;
  showManaReceived?: boolean;
};

export function visibleTimelineEvents(
  timeline: ReportTimeline,
  hidden: Set<number>,
  {
    showCasts,
    showBuffs,
    showDeaths,
    showDirectHeals = false,
    showHots = false,
    showDamageTaken = false,
    showManaSpent = false,
    showManaReceived = false
  }: TimelineVisibilityOptions
): TimelineEvent[] {
  return timeline.events
    .filter((event) =>
      event.kind === 'death'
        ? showDeaths
        : !hidden.has(event.spellId) &&
          (((event.kind === 'cast' || event.kind === 'marker') && showCasts) ||
            (event.kind === 'buff' && showBuffs) ||
            (event.kind === 'heal' && showDirectHeals) ||
            (event.kind === 'hot' && showHots) ||
            (event.kind === 'damage' && showDamageTaken) ||
            (event.kind === 'mana-spent' &&
              (showManaSpent || (showDamageTaken && event.isManaDrain))) ||
            (event.kind === 'mana-returned' && showManaReceived))
    )
    .map((event) =>
      event.timestamp < 0 && event.duration !== null
        ? { ...event, timestamp: 0, duration: Math.max(0, event.duration + event.timestamp) }
        : event
    );
}

export function timelineEventTop(event: TimelineEvent): number {
  if (event.displayBelow) return 75;
  return event.kind === 'buff' ? 47 : event.kind === 'death' ? 28 : 19;
}
export function fightDuration(report: RankingReport, timeline?: ReportTimeline): number {
  return timeline?.duration || report.fights?.[0]?.duration || report.duration || 0;
}
