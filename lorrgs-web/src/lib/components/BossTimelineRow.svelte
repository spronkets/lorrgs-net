<script lang="ts">
  import { Skull } from '@lucide/svelte';
  import Alert from './Alert.svelte';

  import Spinner from './Spinner.svelte';
  import { untrack } from 'svelte';
  import { timelineErrorSummary } from '../timelineError';
  import TimelinePhases from './TimelinePhases.svelte';
  import BossIcon from './BossIcon.svelte';
  import { getBossTimeline } from '../api';
  import type { BossTimeline } from '../models/BossTimeline';
  import type { RankingReport } from '../models/RankingReport';
  import type { TimelineMagnification } from '../models/TimelineMagnification';
  import { withResourceLanes } from '../spellClassification';
  import { spellIconUrl, timelineTime, timelinePreview, TIMELINE_ZOOM_LEVELS,
    visibleTimelineEvents, timelineEventTop, timelineEventDetail, resourceEventColor,
    spellAppearance, isResourceHit } from '../timelineUtils';

  type Props = {
    compositionMode?: boolean;
    report: RankingReport;
    edition: string;
    axisDuration: number;
    ticks: number[];
    showIcons: boolean;
    showCasts: boolean;
    showBuffs?: boolean;
    showDeaths?: boolean;
    showDirectHeals?: boolean;
    showHots?: boolean;
    showDamageTaken?: boolean;
    showManaReceived?: boolean;
    showReceivedBuffs?: boolean;
    hidden?: Set<number>;
    zoom?: number;
    magnifier?: TimelineMagnification | null;
    bossSlug?: string;
    bossIconName?: string;
    onloaded?: (_timeline: BossTimeline) => void;
  };
  let {
    compositionMode = false,
    report,
    edition,
    axisDuration,
    ticks,
    showIcons,
    showCasts,
    showBuffs = true,
    showDeaths = true,
    showDirectHeals = false,
    showHots = false,
    showDamageTaken = false,
    showManaReceived = false,
    showReceivedBuffs = false,
    hidden = new Set(),
    zoom = 100,
    magnifier = $bindable(null),
    bossSlug = '',
    bossIconName,
    onloaded
  }: Props = $props();
  let timeline = $state<BossTimeline | null>(null);
  let loading = $state(false);
  let error = $state('');
  let attempt = $state(0);
  const previewTimeline = $derived(timeline ? {
    ...timeline,
    sourceId: 0,
    playerName: timeline.name
  } : null);
  const events = $derived(previewTimeline ? withResourceLanes(
    visibleTimelineEvents(previewTimeline, hidden, {
      showCasts, showBuffs, showDeaths, showDirectHeals, showHots, showDamageTaken, showManaReceived
    }), previewTimeline.spells, previewTimeline.events, showReceivedBuffs, previewTimeline.playerName
  ) : []);
  const height = $derived(events.some((event) => event.displayBelow) ? 108 : 68);
  function updateMagnifier(mouse: MouseEvent) {
    if (!previewTimeline || !(mouse.currentTarget instanceof HTMLElement) ||
        zoom === TIMELINE_ZOOM_LEVELS.at(-1)) {
      magnifier = null;
      return;
    }
    const track = mouse.currentTarget.closest('.track');
    if (!(track instanceof HTMLElement)) return;
    const eventElement = mouse.target instanceof Element
      ? mouse.target.closest('[data-event-index]') : null;
    magnifier = {
      timeline: previewTimeline,
      boss: true,
      events,
      mergedResources: true,
      hoveredEvent: eventElement
        ? events[Number(eventElement.getAttribute('data-event-index'))] : undefined,
      phases: timeline?.phases,
      showIcons,
      ...timelinePreview(track.getBoundingClientRect(), mouse.clientX,
        previewTimeline.duration, axisDuration, zoom, window.innerWidth),
      x: mouse.clientX,
      y: mouse.clientY
    };
  }
  $effect(() => {
    const current = report;
    const currentEdition = edition;
    void attempt;
    const controller = new AbortController();
    const bundled = current.bossTimeline?.isComplete ? current.bossTimeline : null;
    timeline = bundled;
    error = '';
    loading = bundled === null;
    if (bundled) untrack(() => onloaded?.(bundled));
    if (!bundled) {
      getBossTimeline(current, currentEdition, controller.signal)
        .then((result) => {
          if (!controller.signal.aborted) {
            timeline = result;
            onloaded?.(result);
          }
        })
        .catch((failure: unknown) => {
          if (controller.signal.aborted) return;
          error = failure instanceof Error ? failure.message : 'Unable to load boss timeline.';
          console.error('Boss reference timeline failed', {
            reportCode: current.reportId,
            failure
          });
        })
        .finally(() => {
          if (!controller.signal.aborted) loading = false;
        });
    }
    return () => controller.abort();
  });
</script>

<div class="boss-row" aria-label="Boss reference timeline">
  {#if !compositionMode}
  <div class="position-placeholder" aria-hidden="true">
    <Skull size={20} aria-hidden="true" />
  </div>
  {/if}
  <div class="identity">
    <div class="boss-heading"
      title="Boss abilities and phases from the selected reference report."
    >
      <BossIcon slug={bossSlug} iconName={bossIconName} size={24} />
      <span>{timeline?.name || report.fights?.[0]?.name || 'Boss'}</span>
    </div>
  </div>
  <div class="track" role="group" aria-label="Boss ability timeline"
    style:height={`${height}px`}
    onmousemove={updateMagnifier}
    onblur={() => { if (magnifier) magnifier = null; }}
    onmouseout={(mouse) => {
      if (!(mouse.relatedTarget instanceof Node) || !mouse.currentTarget.contains(mouse.relatedTarget))
        magnifier = null;
    }}>
    {#each ticks as tick (tick)}
      <span class="gridline" style={`left: ${(tick / axisDuration) * 100}%`}></span>
    {/each}
    {#if loading}
      <span class="message" role="status"><Spinner inline />Loading boss events…</span>
    {:else if error}
      <div class="message">
        <Alert variant="warning" compact>
          <span>{timelineErrorSummary(error)}</span>
          <button
            class="button"
            data-tone="warning"
            data-outline="true"
            data-size="small"
            type="button"
            onclick={() => attempt++}>Retry boss timeline</button
          >
        </Alert>
      </div>
    {:else if timeline}
      <TimelinePhases phases={timeline.phases} {axisDuration} />
      {#if magnifier?.timeline.reportCode === timeline.reportCode &&
        magnifier.timeline.fightId === timeline.fightId && magnifier.timeline.sourceId === 0}
        <span class="magnified-section"
          style={`left: ${magnifier.start / axisDuration * 100}%; width: ${magnifier.span / axisDuration * 100}%`}
          aria-hidden="true"></span>
      {/if}
        {#each events as event, index (index)}
          {@const spell = timeline.spells.find((item) => item.id === event.spellId)}
          {@const title = event.kind === 'cast'
            ? `${timelineTime(event.timestamp)} · ${spell?.name || `Spell ${event.spellId}`}`
            : timelineEventDetail(event, spell)}
          {@const color = resourceEventColor(event, spell) ?? spellAppearance(spell, event.kind === 'cast').color}
          {#if event.kind === 'cast' || isResourceHit(event) || event.displayBelow}
            <span class="cast-connectors"
              class:below={event.displayBelow}
              class:instant={event.castStart == null || event.castStart === event.timestamp}
              style={`--spell-color: ${color}; left: ${(event.castStart ?? event.timestamp) / axisDuration * 100}%; width: ${(event.timestamp - (event.castStart ?? event.timestamp)) / axisDuration * 100}%; top: ${event.displayBelow ? 47 : timelineEventTop(event)}px`}
              aria-hidden="true"></span>
          {/if}
          {#if event.kind === 'cast' && event.castStart != null || event.displayBelow && event.kind === 'buff'}
            {@const start = event.castStart ?? event.timestamp}
            {@const end = event.kind === 'buff' ? event.timestamp + (event.duration ?? 0) : event.timestamp}
            <span class="cast-window"
              style={`left: ${start / axisDuration * 100}%; width: ${(end - start) / axisDuration * 100}%; --spell-color: ${color}`}
              aria-label={title}></span>
          {/if}
          <button
            class={['button', ['boss-timeline-row-button', 'boss-event',
              { buff: event.kind === 'buff' && !event.displayBelow, death: event.kind === 'death',
                'cast-icon': event.kind === 'cast' || isResourceHit(event) || event.displayBelow }]]}
            data-tone="unstyled"
            data-outline="false"
            data-size="medium"
            type="button"
            data-event-index={index}
            onmouseenter={updateMagnifier}
            style={`left: ${(event.timestamp / axisDuration) * 100}%; top: ${timelineEventTop(event)}px; width: ${event.kind === 'buff' && !event.displayBelow ? `max(2px, ${(event.duration ?? 0) / axisDuration * 100}%)` : event.kind === 'death' ? '2px' : '22px'}; --spell-color: ${color}`}
            aria-label={title}
          >
            {#if event.kind === 'death'}
              <span aria-hidden="true">†</span>
            {:else if event.kind !== 'buff' || event.displayBelow}
            {#if showIcons && spellIconUrl(spell?.icon ?? null)}
              <img src={spellIconUrl(spell?.icon ?? null)} alt="" loading="lazy" />
            {:else}<span aria-hidden="true">◆</span>{/if}
            {/if}
          </button>
        {/each}
      <span
        class="fight-end"
        style={`left: min(calc(100% - 2px), ${(timeline.duration / axisDuration) * 100}%)`}
        role="img"
        aria-label={`Reference fight end at ${timelineTime(timeline.duration)}`}
      >
        <span>{timelineTime(timeline.duration)}</span>
      </span>
      {#if !timeline.isComplete}<div class="incomplete">
          <Alert variant="warning" compact>Partial boss timeline — event limit reached</Alert>
        </div>
      {:else if timeline.events.length === 0}<div class="message">
          <Alert compact>No enemy events were recorded for this fight.</Alert>
        </div>{/if}
    {/if}
  </div>
</div>

<style>
  .boss-row {
    display: flex;
    min-height: 68px;
    border-bottom: 1px solid var(--line);
    background: var(--panel);
  }
  @media (max-width: 600px) {
    .boss-row {
      flex-direction: column;
    }
    .boss-row .identity {
      box-sizing: border-box;
      width: var(--timeline-viewport-width);
      border-right: 0;
      border-bottom: 1px solid var(--line);
    }
    .boss-row .track {
      width: calc(100% - 34px);
    }
  }
  .position-placeholder {
    overflow: hidden;
    display: flex;
    align-items: center;
    justify-content: center;
    width: var(--timeline-position-width, 0px);
    flex-shrink: 0;
    position: sticky;
    left: 0;
    z-index: 4;
    background: var(--panel);
    border-right: 1px solid var(--line);
    box-sizing: border-box;
  }
  .identity {
    display: flex;
    align-items: center;
    width: var(--timeline-identity-width, 220px);
    flex-shrink: 0;
    padding: 6px 8px;
    position: sticky;
    left: var(--timeline-position-width, 0px);
    background: var(--panel);
    border-right: 1px solid var(--line);
    z-index: 3;
  }
  .boss-heading {
    display: flex;
    align-items: center;
    gap: 5px;
    color: var(--text);
    font-size: 13px;
    font-weight: 600;
  }
  .track {
    position: relative;
    z-index: 0;
    overflow-x: clip;
    width: calc(
      100% - var(--timeline-identity-width, 220px) - 28px - var(--timeline-position-width, 0px)
    );
    flex-shrink: 0;
    height: 68px;
  }
  .gridline {
    position: absolute;
    top: 0;
    bottom: 0;
    border-left: 1px solid var(--line);
    opacity: 0.4;
  }
  .message {
    position: absolute;
    left: 8px;
    top: 50%;
    transform: translateY(-50%);
    font-size: 12px;
    z-index: 1;
    max-width: calc(100% - 16px);
  }
  :global(button.boss-timeline-row-button) {
    color: var(--text);
    background: var(--panel);
    border: 1px solid var(--line);
    border-radius: 3px;
    cursor: pointer;
    font: inherit;
  }
  :global(.boss-event.boss-timeline-row-button) {
    position: absolute;
    top: 23px;
    width: 22px;
    height: 22px;
    padding: 0;
    display: grid;
    place-items: center;
    border: 0;
    background: transparent;
    z-index: 2;
  }
  :global(.boss-event.boss-timeline-row-button) img {
    display: block;
    width: 22px;
    height: 22px;
    border: 1px solid var(--spell-color, var(--muted));
    border-radius: 2px;
  }
  :global(.boss-event.buff.boss-timeline-row-button) {
    height: 9px;
    background: var(--spell-color);
  }
  :global(.boss-event.death.boss-timeline-row-button) {
    height: 34px;
    background: #ff6969;
  }
  .cast-window {
    position: absolute;
    top: 47px;
    height: 8px;
    border: 1px solid var(--spell-color);
    background: color-mix(in srgb, var(--spell-color) 25%, transparent);
    pointer-events: none;
  }
  :global(.boss-event.cast-icon.boss-timeline-row-button) {
    transform: translateX(-100%);
  }
  .cast-connectors {
    position: absolute;
    height: 36px;
    border-left: 1px solid var(--spell-color);
    border-right: 1px solid var(--spell-color);
    pointer-events: none;
  }
  .cast-connectors.instant {
    border-left: 0;
  }
  .cast-connectors:not(.instant) {
    border-top: 1px solid var(--spell-color);
  }
  .cast-connectors.below {
    height: 50px;
  }
  .cast-connectors.below:not(.instant) {
    border-top: 0;
    border-bottom: 1px solid var(--spell-color);
  }
  .magnified-section {
    position: absolute;
    top: 0;
    bottom: 0;
    border: 1px solid var(--text);
    background: color-mix(in srgb, var(--text) 18%, transparent);
    pointer-events: none;
    z-index: 4;
  }
  :global(.boss-event.boss-timeline-row-button:focus-visible) {
    outline: 2px solid var(--text);
    outline-offset: 2px;
    z-index: 4;
  }
  .fight-end {
    position: absolute;
    top: 0;
    bottom: 0;
    border-left: 2px dashed var(--muted);
    pointer-events: none;
  }
  .fight-end span {
    position: absolute;
    right: 8px;
    bottom: 2px;
    color: var(--muted);
    font: 10px monospace;
  }
  .incomplete {
    position: absolute;
    bottom: 0;
    left: 8px;
  }
</style>
