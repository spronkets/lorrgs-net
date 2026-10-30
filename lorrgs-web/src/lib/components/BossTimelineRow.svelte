<script lang="ts">
  import Alert from './Alert.svelte';
  import Spinner from './Spinner.svelte';
  import { untrack } from 'svelte';
  import { timelineErrorSummary } from '../timelineError';
  import TimelinePhases from './TimelinePhases.svelte';
  import BossIcon from './BossIcon.svelte';
  import { getBossTimeline } from '../api';
  import type { BossTimeline } from '../models/BossTimeline';
  import type { RankingReport } from '../models/RankingReport';
  import { getRankingReportLink } from '../rankingUtils';
  import { spellIconUrl, timelineTime } from '../timelineUtils';

  type Props = {
    report: RankingReport;
    edition: string;
    axisDuration: number;
    ticks: number[];
    showIcons: boolean;
    showCasts: boolean;
    bossSlug?: string;
    bossIconName?: string;
    onloaded?: (_timeline: BossTimeline) => void;
  };
  let {
    report,
    edition,
    axisDuration,
    ticks,
    showIcons,
    showCasts,
    bossSlug = '',
    bossIconName,
    onloaded
  }: Props = $props();
  let timeline = $state<BossTimeline | null>(null);
  let loading = $state(false);
  let error = $state('');
  let attempt = $state(0);
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
  <div class="position-placeholder" aria-hidden="true"></div>
  <div class="identity">
    <a
      href={getRankingReportLink(report, edition)}
      target="_blank"
      rel="noopener"
      title="Boss abilities and phases from the selected reference report."
    >
      <BossIcon slug={bossSlug} iconName={bossIconName} size={24} />
      <span>{timeline?.name || report.fights?.[0]?.name || 'Boss'}</span>
    </a>
  </div>
  <div class="track">
    {#each ticks as tick (tick)}
      <span class="gridline" style={`left: ${(tick / axisDuration) * 100}%`}></span>
    {/each}
    {#if loading}
      <span class="message" role="status"><Spinner inline />Loading boss events…</span>
    {:else if error}
      <div class="message">
        <Alert variant="error" compact>
          <span>{timelineErrorSummary(error)}</span>
          <button type="button" onclick={() => attempt++}>Retry boss timeline</button>
        </Alert>
      </div>
    {:else if timeline}
      <TimelinePhases phases={timeline.phases} {axisDuration} />
      {#if showCasts}
        {#each timeline.events as event, index (index)}
          {@const spell = timeline.spells.find((item) => item.id === event.spellId)}
          {@const title = `${timelineTime(event.timestamp)} · ${spell?.name || `Spell ${event.spellId}`}`}
          <button
            type="button"
            class="boss-event"
            style={`left: ${(event.timestamp / axisDuration) * 100}%`}
            {title}
            aria-label={title}
          >
            {#if showIcons && spellIconUrl(spell?.icon ?? null)}
              <img src={spellIconUrl(spell?.icon ?? null)} alt="" loading="lazy" />
            {:else}<span aria-hidden="true">◆</span>{/if}
          </button>
        {/each}
      {/if}
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
          <Alert compact>No enemy casts were recorded for this fight.</Alert>
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
  a {
    display: flex;
    align-items: center;
    gap: 5px;
    color: var(--text);
    font-size: 13px;
    font-weight: 600;
    text-decoration: none;
  }
  .track {
    position: relative;
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
  button {
    color: var(--text);
    background: var(--panel);
    border: 1px solid var(--line);
    border-radius: 3px;
    cursor: pointer;
    font: inherit;
  }
  .boss-event {
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
  .boss-event img {
    display: block;
    width: 22px;
    height: 22px;
    border: 1px solid var(--muted);
    border-radius: 2px;
  }
  .boss-event:focus-visible {
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
