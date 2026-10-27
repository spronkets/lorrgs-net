<script lang="ts">
  import { onMount, untrack } from 'svelte';
  import Toast from './Toast.svelte';
  import CopyNoteDialog from './CopyNoteDialog.svelte';
  import SpellSelection from './SpellSelection.svelte';
  import TimelineMagnifier from './TimelineMagnifier.svelte';
  import TimelineRow from './TimelineRow.svelte';
  import BossTimelineRow from './BossTimelineRow.svelte';
  import { TimelineLoader } from '../timelineLoader.svelte';
  import type { InspectedSpell, TimelineMagnification } from '../timelineTypes';
  import type { RankingReport, ReportTimeline } from '../api';
  import {
    TIMELINE_ZOOM_LEVELS,
    canLoadTimeline,
    fightDuration,
    timelineKey,
    timelineTime
  } from '../timelineUtils';
  type Props = {
    reports?: { report: RankingReport; rank: number }[];
    edition?: string;
    classId?: number;
    bossSlug?: string;
    bossIconName?: string;
    difficulty?: string;
    specSlug?: string;
  };
  let {
    reports = [],
    edition = '',
    classId = 0,
    bossSlug = '',
    bossIconName,
    difficulty = 'mythic',
    specSlug = ''
  }: Props = $props();
  const loader = new TimelineLoader(() => edition);
  let hidden = $state<Set<number>>(new Set());
  let scrollContainer: HTMLDivElement;
  let previousFirstKey = '';
  let referenceKey = $state('');
  const referenceReport = $derived(
    reports.find(({ report }) => timelineKey(report) === referenceKey)?.report ?? reports[0]?.report
  );
  let zoom = $state<number>(TIMELINE_ZOOM_LEVELS[0]);
  let showIcons = $state(true);
  let showCasts = $state(true);
  let showBuffs = $state(true);
  let showDeaths = $state(true);
  let separateSpells = $state(false);
  let clipboardNotification = $state<{ message: string; variant: 'success' | 'error' } | null>(
    null
  );
  let inspected = $state<InspectedSpell | null>(null);
  let noteSelection = $state<{ timeline: ReportTimeline; report: RankingReport } | null>(null);
  let magnifier = $state<TimelineMagnification | null>(null);
  $effect(() => {
    if (separateSpells || zoom === TIMELINE_ZOOM_LEVELS.at(-1)) magnifier = null;
  });
  const timelines = $derived([...loader.loaded.values()]);
  const selectedClassId = $derived(classId || reports[0]?.report.players?.[0]?.classId || 0);
  const castSpellIds = $derived(
    new Set(
      timelines.flatMap((timeline) =>
        timeline.events.filter((event) => event.kind === 'cast').map((event) => event.spellId)
      )
    )
  );
  const axisDuration = $derived(
    Math.max(
      1,
      ...reports.map(({ report }) => fightDuration(report, loader.loaded.get(timelineKey(report))))
    )
  );
  const ticks = $derived(
    Array.from({ length: Math.floor(axisDuration / 30000) + 1 }, (_, i) => i * 30000)
  );
  const labelTicks = $derived([
    ...ticks.filter((tick) => tick < axisDuration - 15000),
    axisDuration
  ]);
  const queue = $derived(reports.map(({ report }) => report).filter(canLoadTimeline));
  const pending = $derived(queue.filter((report) => !loader.loaded.has(timelineKey(report))));
  $effect(() => {
    const current = queue;
    untrack(() => loader.setReports(current));
  });
  $effect(() => {
    const firstKey = reports[0] ? timelineKey(reports[0].report) : '';
    if (firstKey !== previousFirstKey) {
      previousFirstKey = firstKey;
      if (scrollContainer) scrollContainer.scrollTop = 0;
    }
  });
  onMount(() => {
    loader.start();
    return () => loader.dispose();
  });
</script>

<section class="workspace" aria-label="Cooldown comparison timelines">
  {#if noteSelection}
    <CopyNoteDialog
      {...noteSelection}
      {hidden}
      {difficulty}
      {specSlug}
      bossName={noteSelection.report.fights?.find(
        (fight) => fight.fightId === noteSelection.timeline.fightId
      )?.name ?? ''}
      onclose={() => (noteSelection = null)}
      oncopied={(name) => {
        clipboardNotification = {
          message: `Copied selected casts for ${name}.`,
          variant: 'success'
        };
        noteSelection = null;
      }}
    />
  {/if}
  <div class="timeline-controls">
    <fieldset>
      <legend>Timeline</legend>
      {#each [{ name: 'Spell icons', key: 'icons' }, { name: 'Casts', key: 'casts' }, { name: 'Buff durations', key: 'buffs' }, { name: 'Deaths', key: 'deaths' }, { name: 'Separate spells', key: 'lanes' }] as option (option.key)}
        <label class="option-toggle" title={option.name}>
          <input
            type="checkbox"
            aria-label={option.name}
            checked={option.key === 'icons'
              ? showIcons
              : option.key === 'casts'
                ? showCasts
                : option.key === 'buffs'
                  ? showBuffs
                  : option.key === 'deaths'
                    ? showDeaths
                    : separateSpells}
            onchange={(event) => {
              const checked = event.currentTarget.checked;
              if (option.key === 'icons') showIcons = checked;
              else if (option.key === 'casts') showCasts = checked;
              else if (option.key === 'buffs') showBuffs = checked;
              else if (option.key === 'deaths') showDeaths = checked;
              else separateSpells = checked;
            }}
          />
          <span aria-hidden="true">
            <svg
              width="18"
              height="18"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              {#if option.key === 'icons'}
                <rect x="3" y="3" width="18" height="18" rx="2" />
                <path d="m3 13 5-5 13 13M12 12l4-4 5 5" />
              {:else if option.key === 'casts'}
                <circle cx="12" cy="12" r="9" />
                <path d="M12 6v6l4 2" />
              {:else if option.key === 'buffs'}
                <rect x="3" y="7" width="18" height="10" rx="2" />
                <path d="M7 10v4m4-4v4" />
              {:else if option.key === 'deaths'}
                <path d="M12 3v18M6 9h12" />
              {:else}
                <path d="M9 5h12M9 12h12M9 19h12M3 5h1M3 12h1M3 19h1" />
              {/if}
            </svg>
          </span>
        </label>
      {/each}
    </fieldset>
    <label class="zoom"
      >Zoom
      <select bind:value={zoom}>
        {#each TIMELINE_ZOOM_LEVELS as level (level)}
          <option value={level}>{level}%</option>
        {/each}
      </select>
    </label>
    <button
      type="button"
      disabled={loader.batchLoading || pending.length === 0}
      onclick={() => loader.loadNext()}
    >
      {loader.batchLoading
        ? 'Loading timelines…'
        : loader.stopped
          ? 'Resume timeline loading'
          : pending.length
            ? 'Load next 3 timelines'
            : 'All timelines loaded'}
    </button>
    <span class="load-progress" role="status"
      >{queue.filter((report) => loader.loaded.has(timelineKey(report))).length} / {queue.length} timelines
      ready</span
    >
  </div>
  <SpellSelection {timelines} classId={selectedClassId} {edition} bind:hidden bind:inspected />
  {#if loader.stopped}
    <p class="load-error" role="alert">
      Timeline loading paused after an error. Remaining reports were not requested. Use Retry or
      Resume timeline loading after resolving the error.
    </p>
  {/if}
  {#if clipboardNotification}
    {#key clipboardNotification}
      <Toast
        message={clipboardNotification.message}
        variant={clipboardNotification.variant}
        onclose={() => (clipboardNotification = null)}
      />
    {/key}
  {/if}
  <TimelineMagnifier
    {magnifier}
    {inspected}
    {hidden}
    {castSpellIds}
    {showCasts}
    {showBuffs}
    {showDeaths}
  />
  <div class="timeline-panel">
    <div class="spell-inspector" aria-live="polite" aria-atomic="true">
      {#if inspected}
        <span class="color-key" style={`background: ${inspected.color}`}></span>
        <strong>{inspected.name}</strong>
        <span>{inspected.detail}</span>
      {:else}
        <span class="legend-item"><i class="color-key power-key"></i>Power windows</span>
        <span class="legend-item"><i class="color-key ability-key"></i>Player abilities</span>
        <span class="legend-item"><i class="color-key received-key"></i>Received effects</span>
        <span
          >Hover or focus a spell to highlight it across players. Tab to inspect individual events.</span
        >
      {/if}
    </div>
    <div
      class="timeline-scroll"
      bind:this={scrollContainer}
      role="region"
      aria-label="Aligned player timelines"
    >
      <div class="timeline-grid" style={`width: calc(${zoom}% - ${248 * (zoom / 100 - 1)}px)`}>
        <div class="axis-row">
          <div class="identity axis-label">Player / elapsed time</div>
          <div class="axis">
            {#each labelTicks as tick (tick)}
              <span
                class:end-label={tick === axisDuration}
                style={`left: ${(tick / axisDuration) * 100}%`}>{timelineTime(tick)}</span
              >
            {/each}
          </div>
        </div>
        {#if referenceReport && canLoadTimeline(referenceReport)}
          <BossTimelineRow
            {bossSlug}
            {bossIconName}
            report={referenceReport}
            {edition}
            {axisDuration}
            {ticks}
            {showIcons}
            {showCasts}
          />
        {/if}
        {#each reports as { report, rank } (timelineKey(report))}
          <TimelineRow
            {report}
            isReference={referenceReport !== undefined &&
              timelineKey(report) === timelineKey(referenceReport)}
            onreference={(selected) => (referenceKey = timelineKey(selected))}
            {rank}
            {edition}
            timeline={loader.loaded.get(timelineKey(report))}
            {axisDuration}
            {ticks}
            {hidden}
            {castSpellIds}
            {zoom}
            {showIcons}
            {showCasts}
            {showBuffs}
            {showDeaths}
            {separateSpells}
            loading={loader.loadingKey === timelineKey(report)}
            batchLoading={loader.batchLoading}
            error={loader.errors.get(timelineKey(report))}
            bind:inspected
            bind:magnifier
            onload={(report) => loader.loadOne(report)}
            oncopy={(timeline) => (noteSelection = { timeline, report })}
          />
        {:else}
          <p>No loaded reports match these filters.</p>
        {/each}
      </div>
    </div>
  </div>
</section>

<style>
  .workspace {
    --panel: #f4f6f8;
    --line: #bdc9d4;
    --text: #182c40;
    --muted: #536477;
    --accent: #a32662;
    color: var(--text);
    display: flex;
    flex-direction: column;
    gap: 8px;
    min-width: 0;
  }
  .option-toggle {
    position: relative;
  }
  .timeline-panel {
    min-width: 0;
  }
  .option-toggle input {
    position: absolute;
    width: 100%;
    height: 100%;
    margin: 0;
    opacity: 0;
    cursor: pointer;
  }
  .option-toggle span {
    display: grid;
    place-items: center;
    width: 30px;
    height: 28px;
    border: 1px solid var(--line);
    border-radius: 3px;
    opacity: 0.45;
  }
  .option-toggle svg {
    display: block;
  }
  .option-toggle input:checked + span {
    opacity: 1;
    background: var(--line);
  }
  .option-toggle input:focus-visible + span {
    outline: 2px solid var(--text);
    outline-offset: 2px;
  }
  .spell-inspector {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 8px;
    min-height: 36px;
    padding: 8px 10px;
    background: var(--panel);
    border: 1px solid var(--line);
    border-bottom: 0;
    border-radius: 4px 4px 0 0;
    font-size: 11px;
    color: var(--muted);
  }
  .spell-inspector strong {
    color: var(--text);
  }
  .legend-item {
    display: inline-flex;
    gap: 4px;
    align-items: center;
  }
  .color-key {
    display: inline-block;
    width: 9px;
    height: 9px;
    border: 1px solid #536477;
    border-radius: 2px;
  }
  .power-key {
    background: #f4bd50;
  }
  .ability-key {
    background: #69bafa;
  }
  .received-key {
    background: #bba0ee;
  }
  :global(.theme-dark) .workspace {
    --panel: #20201e;
    --line: #454540;
    --text: #eeeae4;
    --muted: #b8b6ae;
    --accent: #f48cba;
  }
  .timeline-controls {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 6px;
  }
  .timeline-controls {
    align-items: end;
  }
  .load-progress {
    font-size: 11px;
    color: var(--muted);
    padding: 6px 0;
  }
  fieldset {
    display: flex;
    flex-wrap: wrap;
    gap: 3px;
    border: 1px solid var(--line);
    border-radius: 4px;
    padding: 6px 10px;
    margin: 0;
  }
  legend {
    font-size: 12px;
    font-weight: 600;
  }
  label {
    font-size: 12px;
    display: inline-flex;
    gap: 4px;
    align-items: center;
  }
  .zoom {
    display: grid;
  }
  button,
  select {
    color: var(--text);
    background: var(--panel);
    border: 1px solid var(--line);
    border-radius: 3px;
    padding: 5px 8px;
    font: inherit;
    font-size: 12px;
  }
  button {
    cursor: pointer;
  }
  button:disabled {
    opacity: 0.5;
    cursor: wait;
  }
  button:hover:not(:disabled),
  button:focus-visible {
    border-color: var(--accent);
  }
  .timeline-scroll {
    overflow: auto;
    width: 100%;
    max-width: 100%;
    max-height: 70vh;
    border: 1px solid var(--line);
    border-radius: 4px;
    background: var(--panel);
  }
  .axis-row {
    display: flex;
    border-bottom: 1px solid var(--line);
  }
  .axis-row {
    position: sticky;
    top: 0;
    z-index: 5;
    background: var(--panel);
    height: 30px;
  }
  .identity {
    width: 220px;
    flex-shrink: 0;
    padding: 6px 8px;
    position: sticky;
    left: 0;
    background: var(--panel);
    border-right: 1px solid var(--line);
    z-index: 3;
  }
  .axis-label {
    font-size: 11px;
    z-index: 6;
  }
  .axis {
    position: relative;
    width: calc(100% - 248px);
    flex-shrink: 0;
  }
  .axis .end-label {
    transform: translateX(-100%);
    padding-left: 0;
  }
  .axis span {
    position: absolute;
    top: 8px;
    padding-left: 3px;
    font: 11px monospace;
  }
  .load-error {
    font-size: 12px;
    color: #ce4141;
  }
</style>
