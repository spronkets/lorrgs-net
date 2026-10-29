import type { NoteFormat } from './models/NoteFormat';
import type { RaidNoteOptions } from './models/RaidNoteOptions';
import type { RankingReport } from './models/RankingReport';
import type { ReportTimeline } from './models/ReportTimeline';

const specGroups = [
  ['warrior', 71, 72, 73],
  ['paladin', 65, 66, 70],
  ['hunter', 253, 254, 255],
  ['rogue', 259, 260, 261],
  ['priest', 256, 257, 258],
  ['deathknight', 250, 251, 252],
  ['shaman', 262, 263, 264],
  ['mage', 62, 63, 64],
  ['warlock', 265, 266, 267],
  ['monk', 268, 270, 269],
  ['druid', 102, 103, 104, 105],
  ['demonhunter', 577, 581, 1480],
  ['evoker', 1467, 1468, 1473]
] as const;

export function specNoteTag(specId: number, format: NoteFormat): string | null {
  if (format === 'nsrt')
    return specGroups.some(([, ...ids]) => ids.some((id) => id === specId)) ? String(specId) : null;
  for (const [className, ...ids] of specGroups) {
    const index = ids.findIndex((id) => id === specId);
    if (index >= 0) return `spec:${className.toUpperCase()}:${index + 1}`;
  }
  return null;
}

function safeText(value: string): string {
  return value.replace(/[{};\r\n]/g, '').trim();
}

export function buildRaidNote(
  timeline: ReportTimeline,
  report: RankingReport,
  hidden: Set<number>,
  options: RaidNoteOptions
): { text: string; count: number; error: string } {
  const player =
    report.players?.find((player) => player.specSlug === options.specSlug) ??
    report.players?.find((player) => player.name === timeline.playerName);
  const tag =
    options.target === 'spec'
      ? specNoteTag(player?.specId ?? 0, options.format)
      : safeText(options.playerName);
  if (tag === null)
    return {
      text: '',
      count: 0,
      error: 'This specialization has no supported note tag. Use Player Name instead.'
    };
  const fight = report.fights?.find((fight) => fight.fightId === timeline.fightId);
  const encounterId = fight?.encounterId ?? 0;
  if (options.format === 'nsrt' && encounterId <= 0)
    return { text: '', count: 0, error: 'Encounter ID is unavailable. Use MRT format instead.' };
  const bossPhases =
    report.bossTimeline?.fightId === timeline.fightId ? report.bossTimeline.phases : [];
  const phases = [...(bossPhases.length ? bossPhases : (fight?.phases ?? []))]
    .filter((phase) => !phase.isIntermission)
    .map((phase) => ({ ...phase, id: phase.notePhaseId ?? phase.id }))
    .filter(
      (phase) =>
        phase.timestamp >= 0 &&
        phase.timestamp <= timeline.duration &&
        Number.isInteger(phase.id) &&
        phase.id > 0
    )
    .sort((a, b) => a.timestamp - b.timestamp);
  const casts = timeline.events
    .filter(
      (event) =>
        event.kind === 'cast' &&
        !event.isInferred &&
        event.spellId > 0 &&
        event.timestamp >= 0 &&
        event.timestamp <= timeline.duration &&
        !hidden.has(event.spellId)
    )
    .sort((a, b) => a.timestamp - b.timestamp);
  const lines = casts.map((event) => {
    const phase = options.dynamic
      ? phases.findLast((phase) => phase.timestamp <= event.timestamp)
      : undefined;
    const time = Math.round((event.timestamp - (phase?.timestamp ?? 0)) / 100);
    const seconds = (time / 10).toFixed(1);
    if (options.format === 'nsrt')
      return `ph:${phase?.id ?? 1};time:${seconds};${tag ? `tag:${tag};` : ''}spellid:${event.spellId};`;
    const minutes = Math.floor(time / 600)
      .toString()
      .padStart(2, '0');
    const remainder = ((time % 600) / 10).toFixed(1).padStart(4, '0');
    return `{time:${minutes}:${remainder}${phase && (phase.id !== 1 || phase.timestamp > 0) ? `,p${phase.id}` : ''}} - ${tag ? `${tag} ` : ''}{spell:${event.spellId}}`;
  });
  if (options.format === 'nsrt') {
    const difficulty =
      options.difficulty.charAt(0).toUpperCase() + options.difficulty.slice(1).toLowerCase();
    lines.unshift(
      `EncounterID:${encounterId};Difficulty:${safeText(difficulty)};Name:${safeText(options.bossName || fight?.name || 'Boss')};`
    );
  }
  return {
    text: lines.join('\n'),
    count: casts.length,
    error: casts.length ? '' : 'No selected cast events to copy.'
  };
}
