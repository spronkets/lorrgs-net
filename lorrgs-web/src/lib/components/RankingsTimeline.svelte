<script lang="ts">
  import Alert from './Alert.svelte';
  import Spinner from './Spinner.svelte';
  import { onMount, tick, untrack } from 'svelte';
  import Toast from './Toast.svelte';
  import CopyNoteDialog from './CopyNoteDialog.svelte';
  import SpellSelection from './SpellSelection.svelte';
  import TimelineMagnifier from './TimelineMagnifier.svelte';
  import TimelineRow from './TimelineRow.svelte';
  import BossTimelineRow from './BossTimelineRow.svelte';
  import { TimelineLoader } from '../timelineLoader.svelte';
  import type { InspectedSpell } from '../models/InspectedSpell';
  import type { TimelineMagnification } from '../models/TimelineMagnification';
  import type { BossTimeline } from '../models/BossTimeline';
  import type { RankingReport } from '../models/RankingReport';
  import type { ReportTimeline } from '../models/ReportTimeline';
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
    compositionMode?: boolean;
  };
  let {
    reports = [],
    edition = '',
    classId = 0,
    bossSlug = '',
    bossIconName,
    difficulty = 'mythic',
    specSlug = '',
    compositionMode = false
  }: Props = $props();
  const loader = new TimelineLoader(() => edition);
  let hidden = $state<Set<number>>(new Set());
  let scrollContainer: HTMLDivElement;
  let viewportWidth = $state(0);
  let identityWidth = $state(220);
  $effect(() => {
    reports;
    let disposed = false;
    let observer: ResizeObserver | undefined;
    void tick().then(() => {
      if (disposed || !scrollContainer) return;
      const details = scrollContainer.querySelectorAll<HTMLElement>(
        '.player-details, .player-heading'
      );
      const measure = () => {
        identityWidth = Math.max(
          220,
          ...Array.from(details, (element) => {
            const identity = element.parentElement;
            const inset = identity
              ? identity.offsetWidth -
                identity.clientWidth +
                (parseFloat(getComputedStyle(identity).paddingLeft) || 0) +
                (parseFloat(getComputedStyle(identity).paddingRight) || 0)
              : 0;
            const children = Array.from(element.children);
            const gap = parseFloat(getComputedStyle(element).columnGap) || 0;
            const contentWidth =
              children.reduce((width, child) => width + child.getBoundingClientRect().width, 0) +
              Math.max(0, children.length - 1) * gap;
            return Math.ceil(contentWidth + inset);
          })
        );
      };
      measure();
      const resizeObserver = new ResizeObserver(measure);
      observer = resizeObserver;
      details.forEach((element) => resizeObserver.observe(element));
      details.forEach((element) =>
        Array.from(element.children).forEach((child) => resizeObserver.observe(child))
      );
    });
    return () => {
      disposed = true;
      observer?.disconnect();
    };
  });
  let previousFirstKey = '';
  let referenceKey = $state('');
  let bossReferences = $state<Record<string, BossTimeline>>({});
  function reportWithReference(report: RankingReport): RankingReport {
    const bossTimeline = bossReferences[timelineKey(report)];
    return bossTimeline ? { ...report, bossTimeline } : report;
  }
  const referenceReport = $derived(
    reports.find(({ report }) => timelineKey(report) === referenceKey)?.report ?? reports[0]?.report
  );
  function roleGroup(report: RankingReport) {
    const role = report.players?.[0]?.roleCode;
    return role === 'tank'
      ? 'Tanks'
      : role === 'heal'
        ? 'Healers'
        : role === 'mdps' || role === 'rdps'
          ? 'DPS'
          : 'Unknown role';
  }
  const timelineGroups = $derived(
    compositionMode
      ? ['Tanks', 'Healers', 'DPS', 'Unknown role']
          .map((name) => ({
            name,
            rows: reports.filter(({ report }) => roleGroup(report) === name)
          }))
          .filter((group) => group.rows.length > 0)
      : [{ name: '', rows: reports }]
  );
  let zoom = $state<number>(TIMELINE_ZOOM_LEVELS[0]);
  let showIcons = $state(true);
  let showCasts = $state(true);
  let showBuffs = $state(true);
  let showDeaths = $state(true);
  let showDirectHeals = $state(false);
  const showHots = $derived(showDirectHeals);
  let showDamageTaken = $state(false);
  let showManaReceived = $state(false);
  let showReceivedBuffs = $state(false);
  let clipboardNotification = $state<{ message: string; variant: 'success' | 'error' } | null>(
    null
  );
  let inspected = $state<InspectedSpell | null>(null);
  let noteSelection = $state<{ timeline: ReportTimeline; report: RankingReport } | null>(null);
  let magnifier = $state<TimelineMagnification | null>(null);
  $effect(() => {
    if (zoom === TIMELINE_ZOOM_LEVELS.at(-1)) magnifier = null;
  });
  $effect(() => {
    // Discard the hovered row snapshot when its visible event selection changes.
    void [
      hidden,
      showCasts,
      showBuffs,
      showDeaths,
      showDirectHeals,
      showHots,
      showDamageTaken,
      showManaReceived,
      showReceivedBuffs
    ];
    magnifier = null;
  });
  const timelines = $derived(
    reports.flatMap(({ report }) => {
      const timeline = loader.loaded.get(timelineKey(report));
      return timeline ? [timeline] : [];
    })
  );
  const selectableTimelines = $derived(
    timelines.map((timeline) => {
      const events = timeline.events.filter((event) =>
        event.kind === 'mana-spent'
          ? showDamageTaken && event.isManaDrain
          : event.kind === 'mana-returned'
            ? showManaReceived
          : event.kind === 'heal'
            ? showDirectHeals
            : event.kind === 'hot'
              ? showHots
              : event.kind === 'damage'
                ? showDamageTaken
                : true
      );
      const healingIds = new Set(
        timeline.events
          .filter(
            (event) =>
              event.kind === 'heal' ||
              event.kind === 'hot' ||
              event.kind === 'damage' ||
              event.kind === 'mana-spent' ||
              event.kind === 'mana-returned'
          )
          .map((event) => event.spellId)
      );
      const visibleIds = new Set(events.map((event) => event.spellId));
      return {
        ...timeline,
        events,
        spells: timeline.spells.filter(
          (spell) => !healingIds.has(spell.id) || visibleIds.has(spell.id)
        )
      };
    })
  );
  const selectedClassId = $derived(
    compositionMode ? 0 : classId || reports[0]?.report.players?.[0]?.classId || 0
  );
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
  const labelStep = $derived(
    viewportWidth > 0 && viewportWidth <= 600
      ? Math.max(
          30000,
          Math.ceil(
            axisDuration / Math.max(1, Math.floor(((viewportWidth - 34) * zoom) / 100 / 60)) / 30000
          ) * 30000
        )
      : 30000
  );
  const labelTicks = $derived([
    ...ticks.filter((tick) => tick % labelStep === 0 && tick < axisDuration - labelStep / 2),
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
      report={reportWithReference(noteSelection.report)}
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
      {#each [{ name: 'Spell icons', key: 'icons' }, { name: 'Casts', key: 'casts' }, { name: 'Buff durations', key: 'buffs' }, { name: 'Deaths', key: 'deaths' }, { name: 'Show received buffs', key: 'received-buffs' }, { name: 'Show healing received', key: 'health' }, { name: 'Show damage taken', key: 'damage' }, { name: 'Show mana received', key: 'mana-received' }] as option (option.key)}
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
                    : option.key === 'health'
                      ? showDirectHeals
                      : option.key === 'received-buffs'
                        ? showReceivedBuffs
                        : option.key === 'damage'
                        ? showDamageTaken
                        : showManaReceived}
            onchange={(event) => {
              const checked = event.currentTarget.checked;
              if (option.key === 'icons') showIcons = checked;
              else if (option.key === 'casts') showCasts = checked;
              else if (option.key === 'buffs') showBuffs = checked;
              else if (option.key === 'deaths') showDeaths = checked;
              else if (option.key === 'health') showDirectHeals = checked;
              else if (option.key === 'received-buffs') showReceivedBuffs = checked;
              else if (option.key === 'damage') showDamageTaken = checked;
              else if (option.key === 'mana-received') showManaReceived = checked;
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
              {:else if option.key === 'health'}
                <path d="M20.8 4.6a5.5 5.5 0 0 0-7.8 0L12 5.7l-1.1-1.1a5.5 5.5 0 0 0-7.8 7.8L12 21l8.8-8.6a5.5 5.5 0 0 0 0-7.8Z" />
                <path d="M8 12h8m-4-4v8" />
              {:else if option.key === 'received-buffs'}
                <path d="m12 3 2.5 5.5L20 11l-5.5 2.5L12 19l-2.5-5.5L4 11l5.5-2.5L12 3Zm7 13v6m-3-3h6" />
              {:else if option.key === 'damage'}
                <path d="M12 3s-7 8-7 12a7 7 0 0 0 14 0c0-4-7-12-7-12Z" />
                <path d="M9 16a3 3 0 0 0 3 3" />
              {:else if option.key === 'mana-received'}
                <path d="M9 3h6m-5 0v6l-5 9a2 2 0 0 0 1.8 3h10.4a2 2 0 0 0 1.8-3l-5-9V3M8 15h8" />
                <path d="M12 12v6" />
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
    {#if loader.batchLoading}
      <span role="status" aria-label="Loading timelines"><Spinner inline /></span>
    {:else if pending.length}
      <button type="button" onclick={() => loader.loadNext()}>
        {loader.stopped ? 'Resume timeline loading' : 'Load next 3 timelines'}
      </button>
    {/if}
  </div>
  <SpellSelection
    timelines={selectableTimelines}
    classId={selectedClassId}
    {edition}
    {compositionMode}
    bind:hidden
    bind:inspected
  />
  {#if loader.stopped}
    <Alert variant="warning">
      Further automatic timeline batches are paused. Other in-flight requests can finish. Use Retry
      or Resume timeline loading when you're ready.
    </Alert>
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
    {showDirectHeals}
    {showHots}
    {showDamageTaken}
    {showManaReceived}
  />
  <p class="mobile-timeline-hint">
    Full fight fits at 100%. Increase Zoom to inspect crowded casts.
  </p>
  <div class="timeline-panel composition-panel">
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
      bind:clientWidth={viewportWidth}
      role="region"
      aria-label="Aligned player timelines"
    >
      <div
        class="timeline-grid grouped-grid"
        style={`--timeline-zoom: ${zoom}; --timeline-position-column-width: 24px; --timeline-position-width: ${compositionMode ? '0px' : 'calc(var(--timeline-position-column-width) + 8px)'}; --timeline-identity-width: ${identityWidth}px; --timeline-viewport-width: ${viewportWidth ? `${viewportWidth}px` : '100%'}`}
      >
        {#snippet timeAxis(label = 'Player')}
          <div class="axis-row">
            {#if !compositionMode}<div class="position-label">#</div>{/if}
            <div class="identity axis-label">
              <span>{label}</span>
            </div>
            <div class="axis">
              {#each labelTicks as tick (tick)}
                <span
                  class:end-label={tick === axisDuration}
                  style={`left: ${(tick / axisDuration) * 100}%`}>{timelineTime(tick)}</span
                >
              {/each}
            </div>
          </div>
        {/snippet}
        {#snippet bossRow()}
          {#if referenceReport && canLoadTimeline(referenceReport)}
            <BossTimelineRow
              {bossSlug}
              {bossIconName}
              report={referenceReport}
              onloaded={(timeline) => {
                const matches = reports.filter(
                  ({ report }) =>
                    report.reportId === timeline.reportCode &&
                    report.fights?.[0]?.fightId === timeline.fightId
                );
                for (const { report } of matches) bossReferences[timelineKey(report)] = timeline;
              }}
              {edition}
              {axisDuration}
              {ticks}
              {showIcons}
              {showCasts}
            />
          {/if}
        {/snippet}
        {#if compositionMode}
          <div class="role-timelines">
            {@render timeAxis('Boss')}
            {@render bossRow()}
          </div>
        {:else}
          <section aria-label="Boss timelines" class="role-timelines">
            {@render timeAxis('Boss')}
            {@render bossRow()}
          </section>
        {/if}
        {#each timelineGroups as group (group.name)}
          {#snippet groupRows()}
            {#each group.rows as { report, rank } (timelineKey(report))}
              <TimelineRow
                mergedResources
                {compositionMode}
                report={reportWithReference(report)}
                isReference={referenceReport !== undefined &&
                  timelineKey(report) === timelineKey(referenceReport)}
                onreference={compositionMode
                  ? undefined
                  : (selected) => (referenceKey = timelineKey(selected))}
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
                {showDirectHeals}
                {showHots}
                {showDamageTaken}
                {showManaReceived}
                {showReceivedBuffs}
                loading={loader.loadingKeys.has(timelineKey(report))}
                error={loader.errors.get(timelineKey(report))}
                bind:inspected
                bind:magnifier
                onload={(report) => loader.loadOne(report)}
                oncopy={(timeline) =>
                  (noteSelection = { timeline, report: reportWithReference(report) })}
              />
            {/each}
          {/snippet}
          {#if compositionMode}
            <section aria-label={group.name}>
              <h3 class="role-group">{group.name}</h3>
              <div class="role-timelines">
                {@render timeAxis()}
                {@render groupRows()}
              </div>
            </section>
          {:else}
            <section aria-label="Player timelines" class="role-timelines">
              {@render timeAxis()}
              {@render groupRows()}
            </section>
          {/if}
        {/each}
        {#if !reports.length}<Alert>No loaded reports match these filters.</Alert>{/if}
      </div>
    </div>
  </div>
</section>

<style lang="scss">
  @use '../styles/selection-tokens' as selectionTokens;
  .workspace {
    @include selectionTokens.apply-selection-tokens;
  }

  .role-timelines {
    border: 1px solid var(--line);
    border-radius: 0.4rem;
    background: var(--panel);
  }
  .grouped-grid {
    display: flex;
    flex-direction: column;
    gap: 0.8rem;
    padding-bottom: 1px;
  }
  .composition-panel .timeline-scroll {
    border: 0;
    border-radius: 0;
    background: transparent;
  }
  .composition-panel .spell-inspector {
    margin-bottom: 0.8rem;
    border-bottom: 1px solid var(--line);
    border-radius: 0.4rem;
  }
  .role-group {
    position: sticky;
    left: 0;
    box-sizing: border-box;
    width: var(--timeline-viewport-width);
    margin: 0 0 0.6rem;
    padding: 0;
    font-size: 0.95rem;
  }
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
  .timeline-grid {
    width: calc(
      var(--timeline-zoom) * 1% -
        (var(--timeline-identity-width) + 28px + var(--timeline-position-width)) *
        (var(--timeline-zoom) / 100 - 1)
    );
  }
  .mobile-timeline-hint {
    display: none;
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
    @include selectionTokens.selection-label;

    display: grid;
  }
  button {
    color: var(--text);
    background: var(--panel);
    border: 1px solid var(--line);
    border-radius: 3px;
    padding: 5px 8px;
    font: inherit;
    font-size: 12px;
  }
  select {
    @include selectionTokens.selection-dropdown;
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
    width: var(--timeline-identity-width);
    flex-shrink: 0;
    padding: 6px 8px;
    position: sticky;
    left: var(--timeline-position-width);
    background: var(--panel);
    border-right: 1px solid var(--line);
    z-index: 3;
  }
  .axis-label {
    display: flex;
    align-items: center;
    font-size: 11px;
    z-index: 6;
  }
  .position-label {
    width: var(--timeline-position-width);
    flex-shrink: 0;
    text-align: center;
    position: sticky;
    left: 0;
    z-index: 7;
    background: var(--panel);
    border-right: 1px solid var(--line);
    box-sizing: border-box;
    display: flex;
    align-items: center;
    justify-content: center;
  }
  .axis {
    position: relative;
    width: calc(100% - var(--timeline-identity-width) - 28px - var(--timeline-position-width));
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
  @media (max-width: 600px) {
    .mobile-timeline-hint {
      display: block;
      margin: 0 0 6px;
      font-size: 12px;
      color: var(--muted);
    }
    .timeline-grid {
      width: calc(100% * var(--timeline-zoom) / 100);
    }
    .axis-label {
      display: none;
    }
    .axis {
      width: calc(100% - 34px);
    }
    .timeline-controls fieldset {
      flex: 1 0 100%;
      box-sizing: border-box;
      flex-wrap: nowrap;
    }
    .option-toggle span {
      width: 36px;
      height: 36px;
    }
    .timeline-controls button,
    .timeline-controls select {
      min-height: 40px;
    }
  }
</style>
