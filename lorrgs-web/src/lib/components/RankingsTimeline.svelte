<script lang="ts">
  import { Clock, Cross, Droplet, FlaskConical, HeartPlus, Image, RectangleEllipsis, Sparkles } from '@lucide/svelte';
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
  let timelineWidth = $state(0);
  let headerScrollbar: HTMLDivElement | undefined;
  let navigationHeight = $state(0);
  const synchronizedOffsets = new WeakMap<HTMLElement, number>();
  function syncTimelineScroll(event: Event) {
    const source = event.currentTarget;
    if (!(source instanceof HTMLElement) || event.target !== source) return;
    const synchronizedOffset = synchronizedOffsets.get(source);
    synchronizedOffsets.delete(source);
    if (synchronizedOffset !== undefined && Math.abs(source.scrollLeft - synchronizedOffset) <= 1) return;
    for (const target of [scrollContainer, headerScrollbar]) {
      if (target && target !== source && Math.abs(target.scrollLeft - source.scrollLeft) > 1) {
        target.scrollLeft = source.scrollLeft;
        synchronizedOffsets.set(target, target.scrollLeft);
      }
    }
  }
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
  const viewportDuration = $derived(
    Math.max(
      1,
      ...(compositionMode ? reports : reports.slice(0, 3)).map(({ report }) =>
        fightDuration(report, loader.loaded.get(timelineKey(report)))
      )
    )
  );
  const axisDuration = $derived(
    Math.max(
      viewportDuration,
      ...reports.map(({ report }) => fightDuration(report, loader.loaded.get(timelineKey(report))))
    )
  );
  const timelineScale = $derived(zoom * axisDuration / viewportDuration);
  const ticks = $derived(
    Array.from({ length: Math.floor(axisDuration / 30000) + 1 }, (_, i) => i * 30000)
  );
  const labelStep = $derived(
    viewportWidth > 0 && viewportWidth <= 600
      ? Math.max(
          30000,
          Math.ceil(
            viewportDuration / Math.max(1, Math.floor(((viewportWidth - 34) * zoom) / 100 / 60)) / 30000
          ) * 30000
        )
      : 30000
  );
  const labelTicks = $derived([
    ...ticks.filter((tick) => tick % labelStep === 0 && tick < axisDuration - labelStep / 2),
    axisDuration
  ]);
  const queue = $derived(reports.map(({ report }) => report).filter(canLoadTimeline));
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
    const navigation = document.querySelector('.app-header');
    const observer = new ResizeObserver(() => {
      navigationHeight = navigation?.getBoundingClientRect().height ?? 0;
    });
    if (navigation) {
      navigationHeight = navigation.getBoundingClientRect().height;
      observer.observe(navigation);
    }
    return () => {
      loader.dispose();
      observer.disconnect();
    };
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
        {@const selected =
          option.key === 'icons'
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
        <button
          class={['button', 'option-toggle']}
          data-tone={selected === true || selected === 'true' ? 'primary' : 'secondary'}
          data-outline={!(selected === true || selected === 'true')}
          data-size="small"
          type="button"
          title={option.name}
          aria-label={option.name}
          aria-pressed={selected}
          onclick={() => {
            const checked = !selected;
            if (option.key === 'icons') showIcons = checked;
            else if (option.key === 'casts') showCasts = checked;
            else if (option.key === 'buffs') showBuffs = checked;
            else if (option.key === 'deaths') showDeaths = checked;
            else if (option.key === 'health') showDirectHeals = checked;
            else if (option.key === 'received-buffs') showReceivedBuffs = checked;
            else if (option.key === 'damage') showDamageTaken = checked;
            else if (option.key === 'mana-received') showManaReceived = checked;
          }}
        >
          <span aria-hidden="true">
              {#if option.key === 'icons'}
                <Image size={18} />
              {:else if option.key === 'casts'}
                <Clock size={18} />
              {:else if option.key === 'buffs'}
                <RectangleEllipsis size={18} />
              {:else if option.key === 'deaths'}
                <Cross size={18} />
              {:else if option.key === 'health'}
                <HeartPlus size={18} />
              {:else if option.key === 'received-buffs'}
                <Sparkles size={18} />
              {:else if option.key === 'damage'}
                <Droplet size={18} />
              {:else if option.key === 'mana-received'}
                <FlaskConical size={18} />
              {/if}
          </span>
        </button>
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
      on a failed row or Load report on another row when you're ready.
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
  <div class="timeline-panel composition-panel" class:composition-mode={compositionMode}>
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
    <div class="sticky-timeline-header" style={`top: ${navigationHeight}px; --scrollbar-inset: ${viewportWidth <= 600 ? 0 : identityWidth + (compositionMode ? 0 : 32)}px`}>
      <div class="sticky-identity-headings">
        {#if !compositionMode}<div class="position-label">#</div>{/if}
        <div class="player-label">Player</div>
      </div>
    <!-- svelte-ignore a11y_no_noninteractive_tabindex -->
    <div
      class="sticky-header-scrollbar"
      bind:this={headerScrollbar}
      onscroll={syncTimelineScroll}
      role="region"
      aria-label="Timeline horizontal scroll"
      tabindex="0"
    >
      <div class="time-axis-content" style={`width: ${Math.max(0, timelineWidth - (viewportWidth <= 600 ? 0 : identityWidth + (compositionMode ? 0 : 32)))}px`}>
        <div class="axis">
          {#each labelTicks as tick (tick)}
            <span class:end-label={tick === axisDuration}
              style={`left: ${(tick / axisDuration) * 100}%`}>{timelineTime(tick)}</span>
          {/each}
        </div>
      </div>
    </div>
    </div>
    <div
      class="timeline-scroll"
      bind:this={scrollContainer}
      bind:clientWidth={viewportWidth}
      onscroll={syncTimelineScroll}
      role="region"
      aria-label="Aligned player timelines"
    >
      <div
        class="timeline-grid grouped-grid"
        class:combined-timelines={!compositionMode}
        bind:clientWidth={timelineWidth}
        style={`--timeline-zoom: ${zoom}; --timeline-scale: ${timelineScale}; --timeline-position-column-width: 24px; --timeline-position-width: ${compositionMode ? '0px' : 'calc(var(--timeline-position-column-width) + 8px)'}; --timeline-identity-width: ${identityWidth}px; --timeline-viewport-width: ${viewportWidth ? `${viewportWidth}px` : '100%'}`}
      >
        {#snippet bossRow()}
          {#if referenceReport && canLoadTimeline(referenceReport)}
            <BossTimelineRow
              {compositionMode}
              {bossSlug}
              {bossIconName}
              {zoom}
              bind:magnifier
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
              {showBuffs}
              {showDeaths}
              {showDirectHeals}
              {showHots}
              {showDamageTaken}
              {showManaReceived}
              {showReceivedBuffs}
              {hidden}
            />
          {/if}
        {/snippet}
        {#if compositionMode}
          <div class="role-timelines">
            {@render bossRow()}
          </div>
        {:else}
          {@render bossRow()}
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
                {@render groupRows()}
              </div>
            </section>
          {:else}
            {@render groupRows()}
          {/if}
        {/each}
        {#if !reports.length}
          <Alert>No loaded reports match these filters.</Alert>
        {/if}
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
    border-left: 0;
    border-radius: 0.4rem;
    background: var(--panel);
  }
  .role-timelines :global(.rank),
  .role-timelines :global(.position-placeholder),
  .composition-mode .role-timelines :global(.player-row .identity),
  .composition-mode .role-timelines :global(.boss-row .identity) {
    box-shadow: inset 1px 0 var(--line);
  }
  .grouped-grid {
    display: flex;
    flex-direction: column;
    gap: 0.8rem;
    padding-bottom: 1px;
  }
  .combined-timelines {
    gap: 0;
    border: 1px solid var(--line);
    border-left: 0;
    border-radius: 0.4rem;
    background: var(--panel);
  }
  .combined-timelines :global(.rank),
  .combined-timelines :global(.position-placeholder) {
    box-shadow: inset 1px 0 var(--line);
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
  .timeline-controls :global(.option-toggle) {
    width: 44px;
    padding: 0;
  }
  .timeline-panel {
    min-width: 0;
  }
  .timeline-grid {
    width: calc(
      var(--timeline-scale) * 1% -
        (var(--timeline-identity-width) + 28px + var(--timeline-position-width)) *
        (var(--timeline-scale) / 100 - 1)
    );
  }
  .mobile-timeline-hint {
    display: none;
  }
  .timeline-controls :global(.option-toggle span) {
    display: grid;
    place-items: center;
  }
  .timeline-controls :global(.option-toggle svg) {
    display: block;
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
    border: 0;
    padding: 0;
    margin: 0;
  }
  legend {
    @include selectionTokens.selection-label;

    padding: 0;
    margin-bottom: 4px;
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
  select {
    @include selectionTokens.selection-dropdown;
  }
  .timeline-scroll {
    overflow: auto;
    scrollbar-width: none;
    width: 100%;
    max-width: 100%;
    border: 1px solid var(--line);
    border-radius: 4px;
    background: var(--panel);
  }
  .timeline-scroll::-webkit-scrollbar {
    display: none;
  }
  .sticky-timeline-header {
    position: sticky;
    z-index: 8;
    background: var(--panel);
    box-shadow: inset 0 0 0 1px var(--line);
  }
  .sticky-identity-headings {
    position: absolute;
    inset: 0 auto 0 0;
    display: flex;
    width: var(--scrollbar-inset);
    font-size: 11px;
  }
  .position-label {
    display: grid;
    place-items: center;
    width: 32px;
    flex-shrink: 0;
    border-right: 1px solid var(--line);
  }
  .player-label {
    display: flex;
    align-items: center;
    flex: 1;
    padding: 6px 8px;
    border-right: 1px solid var(--line);
  }
  .sticky-header-scrollbar {
    margin-left: var(--scrollbar-inset);
    overflow-x: auto;
    overflow-y: hidden;
    background: var(--panel);
  }
  .time-axis-content {
    height: 30px;
  }
  .axis {
    position: relative;
    width: calc(100% - 28px);
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
    .sticky-identity-headings {
      display: none;
    }
    .mobile-timeline-hint {
      display: block;
      margin: 0 0 6px;
      font-size: 12px;
      color: var(--muted);
    }
    .timeline-grid {
      width: calc(100% * var(--timeline-scale) / 100);
    }
    .axis {
      width: calc(100% - 34px);
    }
    .timeline-controls fieldset {
      flex: 1 0 100%;
      box-sizing: border-box;
      flex-wrap: nowrap;
    }
    .timeline-controls :global(.option-toggle) {
      width: 44px;
      min-height: 44px;
    }
    .timeline-controls select {
      min-height: 44px;
    }
  }
</style>
