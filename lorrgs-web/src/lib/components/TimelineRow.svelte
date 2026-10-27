<script lang="ts">
  import type { RankingReport, ReportTimeline } from '../api';
  import type { InspectedSpell, TimelineMagnification } from '../timelineTypes';
  import {
    getTopPercentLabel,
    getRankingReportLink,
    getRotationAnalyzerLink
  } from '../rankingUtils';
  import {
    TIMELINE_ZOOM_LEVELS,
    canLoadTimeline,
    castIntervals,
    eventLanes,
    fightDuration,
    isPowerWindow,
    spellAppearance,
    spellIconUrl,
    timelineTime,
    visibleTimelineEvents
  } from '../timelineUtils';
  type Props = {
    report: RankingReport;
    rank: number;
    edition: string;
    timeline?: ReportTimeline;
    axisDuration: number;
    ticks: number[];
    hidden: Set<number>;
    castSpellIds: Set<number>;
    zoom: number;
    showIcons: boolean;
    showCasts: boolean;
    showBuffs: boolean;
    showDeaths: boolean;
    separateSpells: boolean;
    loading: boolean;
    batchLoading: boolean;
    error?: string;
    inspected?: InspectedSpell | null;
    magnifier?: TimelineMagnification | null;
    onload: (_report: RankingReport) => void;
    oncopy: (_timeline: ReportTimeline) => void;
  };
  let {
    report,
    rank,
    edition,
    timeline,
    axisDuration,
    ticks,
    hidden,
    castSpellIds,
    zoom,
    showIcons,
    showCasts,
    showBuffs,
    showDeaths,
    separateSpells,
    loading,
    batchLoading,
    error,
    inspected = $bindable(null),
    magnifier = $bindable(null),
    onload,
    oncopy
  }: Props = $props();
  const endTime = $derived(fightDuration(report, timeline));
  const endLabel = $derived(
    `${report.fights?.[0]?.isKill ? 'Kill' : 'Fight end'} · ${timelineTime(endTime)}`
  );
  const events = $derived(
    timeline
      ? eventLanes(visibleTimelineEvents(timeline, hidden, showCasts, showBuffs, showDeaths))
      : []
  );
  const gaps = $derived(
    inspected
      ? castIntervals(
          events.map(({ event }) => event),
          inspected.id
        )
      : []
  );
  const baseHeight = $derived(
    separateSpells ? Math.max(68, ...events.map(({ lane }) => (lane + 1) * 26 + 16)) : 68
  );
  const height = $derived(baseHeight);
  function reportDate(report: RankingReport): string | null {
    const date = new Date(report.fights?.[0]?.startTime || report.startTime);
    return Number.isFinite(date.getTime()) ? date.toLocaleDateString() : null;
  }
</script>

<div class="player-row" style={`min-height: ${height}px`}>
  <div class="identity">
    <div class="player-heading">
      <span class="rank">{rank}.</span>
      <a
        class="player-report-link"
        href={getRankingReportLink(report, edition)}
        target="_blank"
        rel="noopener"
        title="Open this player's fight in Warcraft Logs in a new tab."
      >
        <span>{timeline?.playerName || report.players?.[0]?.name || 'Unknown player'}</span>
        <svg
          width="11"
          height="11"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          aria-hidden="true"
        >
          <path d="M14 3h7v7m0-7L10 14M10 3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-5" />
        </svg>
      </a>
      <span class="player-performance">
        {(report.players?.[0]?.performance ?? 0).toLocaleString(undefined, {
          maximumFractionDigits: 0
        })} ({getTopPercentLabel(report.percentile)})
      </span>
    </div>
    <div class="player-details">
      <span class="player-meta player-guild" title={report.players?.[0]?.guildName || ''}>
        {report.players?.[0]?.serverRegion || '—'}{#if report.players?.[0]?.guildName}
          - {report.players[0].guildName}{/if}
      </span>
      <span class="player-actions">
        {#if reportDate(report)}<span class="player-meta report-time">{reportDate(report)}</span
          >{/if}
        {#if getRotationAnalyzerLink(report)}
          <a
            class="analyzer-link copy-note"
            href={getRotationAnalyzerLink(report)}
            aria-label="Open report analyzer"
            title="Analyze this player's fight in the report analyzer."
          >
            <svg
              width="13"
              height="13"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              aria-hidden="true"
            >
              <path d="M4 3v18h17M8 16v-5m5 5V7m5 9v-8" />
            </svg>
          </a>
        {/if}
      </span>
    </div>
  </div>
  <div class="track" style={`height: ${height}px`}>
    {#if !timeline && endTime > 0}
      <span
        class="known-duration"
        style={`width: ${(endTime / axisDuration) * 100}%`}
        aria-hidden="true"
      ></span>
    {/if}
    {#each ticks as tick (tick)}
      <span class="gridline" style={`left: ${(tick / axisDuration) * 100}%`}></span>
    {/each}
    {#if endTime > 0}
      <span
        class="after-fight"
        style={`left: ${(endTime / axisDuration) * 100}%; width: ${(1 - endTime / axisDuration) * 100}%`}
        aria-hidden="true"
      ></span>
      <span
        class="fight-end"
        style={`left: min(calc(100% - 2px), ${(endTime / axisDuration) * 100}%)`}
        role="img"
        aria-label={endLabel}
        title={endLabel}><span class="fight-end-label">{timelineTime(endTime)}</span></span
      >
    {/if}
    {#if timeline}
      {#if zoom !== TIMELINE_ZOOM_LEVELS.at(-1) && magnifier?.timeline.reportCode === timeline.reportCode && magnifier.timeline.fightId === timeline.fightId && magnifier.timeline.sourceId === timeline.sourceId}
        <span
          class="magnified-section"
          style={`left: ${(magnifier.start / axisDuration) * 100}%; width: ${(magnifier.span / axisDuration) * 100}%; --spell-color: ${inspected?.color}`}
          aria-hidden="true"
        ></span>
      {/if}
      {#each gaps as gap (gap.start)}
        <span
          class="cast-interval"
          style={`left: ${(gap.start / axisDuration) * 100}%; width: ${((gap.end - gap.start) / axisDuration) * 100}%; top: ${baseHeight - 16}px; --spell-color: ${inspected?.color}`}
          role="img"
          aria-label={`${timeline.playerName} · ${inspected?.name} · ${gap.label} between casts at ${timelineTime(gap.start)} and ${timelineTime(gap.end)}`}
          ><span>{gap.label}</span></span
        >
      {/each}
      {#each events as { event, lane }, index (`${event.kind}-${event.spellId}-${event.timestamp}-${index}`)}
        {@const spell = timeline.spells.find((item) => item.id === event.spellId)}
        {@const icon = spellIconUrl(spell?.icon ?? null)}
        {@const powerWindow = isPowerWindow(spell)}
        {@const appearance = spellAppearance(spell, castSpellIds.has(event.spellId))}
        {@const title = `${timelineTime(event.timestamp)} · ${spell?.name ?? 'Death'}${event.duration !== null ? ` · ${timelineTime(event.duration)} uptime` : ''}${powerWindow ? ' · DPS / power window' : ''}`}
        <button
          type="button"
          class="event"
          class:buff={event.kind === 'buff'}
          class:death={event.kind === 'death'}
          class:cast={event.kind === 'cast'}
          class:power-window={powerWindow}
          class:spell-dimmed={inspected !== null && inspected.id !== event.spellId}
          class:spell-highlighted={inspected?.id === event.spellId}
          style={`--spell-color: ${appearance.color}; left: ${(event.timestamp / axisDuration) * 100}%; top: ${separateSpells ? lane * 26 + 4 : event.kind === 'buff' ? 39 : 8}px; width: ${event.kind === 'buff' ? `max(2px, ${((event.duration ?? 0) / axisDuration) * 100}%)` : event.kind === 'death' ? '2px' : '22px'}`}
          onmouseenter={(mouse) => {
            if (zoom !== TIMELINE_ZOOM_LEVELS.at(-1)) {
              const trackWidth = mouse.currentTarget.parentElement!.getBoundingClientRect().width;
              const previewWidth = Math.min(360, window.innerWidth - 16) - 18;
              const span = Math.min(
                timeline.duration,
                (previewWidth * axisDuration) / Math.max(1, trackWidth * 2)
              );
              const start = Math.max(
                0,
                Math.min(event.timestamp - span / 2, timeline.duration - span)
              );
              magnifier = {
                timeline,
                start,
                span,
                zoom: zoom * 2,
                x: mouse.clientX,
                y: mouse.clientY
              };
            } else {
              magnifier = null;
            }
            inspected = {
              id: event.spellId,
              name: spell?.name ?? 'Death',
              detail: `${timeline.playerName} · ${appearance.label} · ${event.kind} at ${timelineTime(event.timestamp)}${event.duration !== null ? ` · ${timelineTime(event.duration)} measured uptime` : ''}`,
              color: appearance.color
            };
          }}
          onmouseleave={() => {
            inspected = null;
            magnifier = null;
          }}
          onfocus={() =>
            (inspected = {
              id: event.spellId,
              name: spell?.name ?? 'Death',
              detail: `${timeline.playerName} · ${appearance.label} · ${event.kind} at ${timelineTime(event.timestamp)}${event.duration !== null ? ` · ${timelineTime(event.duration)} measured uptime` : ''}`,
              color: appearance.color
            })}
          onblur={() => (inspected = null)}
          {title}
          aria-label={title}
        >
          {#if event.kind === 'cast'}
            {#if showIcons && icon}<img src={icon} alt="" loading="lazy" />
            {:else}<span class="cast-dot">◆</span>{/if}
          {:else if event.kind === 'death'}<span class="death-label">†</span>{/if}
        </button>
      {/each}
      {#if !timeline.isComplete}<span class="incomplete"
          >Partial timeline — event limit reached</span
        >{/if}
      {#if timeline.events.length === 0}
        <span class="track-message"
          >Warcraft Logs returned no cast, buff or death events for this player and fight.</span
        >
      {:else if events.length === 0}<span class="track-message"
          >No events match the spell / timeline selection.</span
        >{/if}
    {:else if loading}
      <span class="track-message loading-message" role="status">Loading report events…</span>
    {:else if error}
      <div class="track-message error-message">
        <span>{error}</span>
        <button type="button" disabled={batchLoading} onclick={() => onload(report)}>Retry</button>
      </div>
    {:else if canLoadTimeline(report)}
      <div class="unloaded-controls">
        <button
          class="load-row"
          type="button"
          disabled={batchLoading}
          onclick={() => onload(report)}
        >
          Load report {report.title || report.reportId}
        </button>
      </div>
    {:else}
      <span class="track-message"
        >Timeline unavailable: report is missing fight or player identity.</span
      >
    {/if}
    {#if timeline}
      <button
        class="copy-note timeline-copy"
        style={`left: calc(${(endTime / axisDuration) * 100}% + 4px)`}
        type="button"
        aria-label="Copy note"
        title="Copy this player's selected spell casts and elapsed timestamps as a plain-text note."
        onclick={() => oncopy(timeline)}
      >
        <svg
          width="13"
          height="13"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          aria-hidden="true"
        >
          <rect x="8" y="8" width="12" height="13" rx="2" />
          <path d="M16 8V5a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v10a2 2 0 0 0 2 2h3" />
        </svg>
      </button>
    {/if}
  </div>
</div>

<style>
  .event.spell-highlighted,
  .event:focus-visible {
    outline: 2px solid var(--text);
    outline-offset: 2px;
    opacity: 1;
    z-index: 4;
  }
  .event.spell-dimmed {
    opacity: 0.18;
  }
  .event.buff.spell-highlighted {
    min-height: 12px;
  }
  .event.buff.power-window {
    background: var(--spell-color);
    opacity: 0.9;
    height: 12px;
    z-index: 3;
    border: 1px solid var(--spell-color);
  }
  .event.cast.power-window img {
    border: 2px solid var(--spell-color);
  }
  .event.power-window .cast-dot {
    color: var(--spell-color);
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
  .player-row {
    display: flex;
    border-bottom: 1px solid var(--line);
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
  .track {
    position: relative;
    width: calc(100% - 248px);
    flex-shrink: 0;
  }
  .track {
    overflow: visible;
  }
  .player-heading {
    display: flex;
    align-items: baseline;
    gap: 5px;
    font-size: 13px;
    font-weight: 600;
  }
  a {
    color: var(--accent);
    text-decoration: none;
  }
  .rank,
  .player-meta {
    font-size: 10px;
    color: var(--muted);
  }
  .player-heading .player-performance {
    margin-left: auto;
  }
  .player-heading a {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    min-width: 0;
  }
  .player-report-link {
    display: inline-flex;
    align-items: center;
    align-self: center;
    gap: 3px;
  }
  .player-report-link span {
    overflow: hidden;
    text-overflow: ellipsis;
  }
  .player-report-link svg {
    display: block;
    align-self: center;
    flex-shrink: 0;
  }
  .player-performance {
    flex-shrink: 0;
  }
  .player-details {
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    gap: 6px;
  }
  .player-guild {
    min-width: 0;
  }
  .player-performance {
    font-size: 10px;
    color: var(--muted);
  }
  .copy-note {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    padding: 3px;
    flex-shrink: 0;
  }
  .player-actions {
    display: inline-flex;
    align-items: center;
    gap: 4px;
    flex-shrink: 0;
  }
  .timeline-copy {
    position: absolute;
    bottom: 2px;
    z-index: 4;
  }
  .analyzer-link {
    border: 1px solid var(--line);
    border-radius: 3px;
  }
  .analyzer-link:hover {
    background: var(--line);
  }
  .analyzer-link:focus-visible {
    outline: 2px solid var(--text);
    outline-offset: 2px;
  }
  .player-meta {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    margin-top: 2px;
  }
  .gridline {
    position: absolute;
    top: 0;
    bottom: 0;
    border-left: 1px solid var(--line);
    opacity: 0.4;
  }
  .fight-end {
    position: absolute;
    top: 0;
    bottom: 0;
    border-left: 2px dashed var(--muted);
    z-index: 1;
    pointer-events: none;
  }
  .fight-end-label {
    position: absolute;
    right: 8px;
    bottom: 2px;
    white-space: nowrap;
    color: var(--muted);
    font: 10px monospace;
    line-height: 12px;
    background: var(--panel);
  }
  .after-fight {
    position: absolute;
    top: 0;
    bottom: 0;
    background: var(--line);
    opacity: 0.2;
    pointer-events: none;
  }
  .event {
    position: absolute;
    display: block;
    padding: 0;
    border: 0;
    border-radius: 2px;
    background: transparent;
    transition:
      opacity 100ms,
      box-shadow 100ms;
  }
  .cast-interval {
    position: absolute;
    height: 14px;
    border: 1px solid var(--spell-color);
    border-top: 0;
    color: var(--spell-color);
    text-align: center;
    pointer-events: none;
    z-index: 3;
    overflow: hidden;
  }
  .cast-interval span {
    padding: 0 2px;
    background: var(--panel);
    font: 10px monospace;
    white-space: nowrap;
  }
  .magnified-section {
    position: absolute;
    top: 0;
    bottom: 0;
    border: 1px solid var(--spell-color);
    background: color-mix(in srgb, var(--spell-color) 18%, transparent);
    pointer-events: none;
  }
  .cast {
    height: 22px;
    z-index: 2;
  }
  .cast img {
    width: 22px;
    height: 22px;
    border: 1px solid var(--spell-color);
    border-radius: 2px;
  }
  .cast-dot {
    color: var(--spell-color);
  }
  .buff {
    height: 8px;
    background: var(--spell-color);
    opacity: 0.5;
    border-radius: 2px;
  }
  .death {
    background: #e35d5d;
    height: 52px;
    top: 2px !important;
    z-index: 2;
  }
  .death-label {
    color: #e35d5d;
    font-size: 20px;
    position: absolute;
    top: 0;
    left: -5px;
  }
  .track-message,
  .unloaded-controls {
    position: absolute;
    display: inline-block;
    left: 8px;
    top: 8px;
    margin: 0;
    font-size: 12px;
  }
  .loading-message,
  .unloaded-controls {
    top: 50%;
    transform: translateY(-50%);
  }
  .unloaded-controls {
    display: flex;
    align-items: center;
    gap: 12px;
    max-width: calc(100% - 16px);
  }
  .load-row {
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
  .known-duration {
    position: absolute;
    top: 0;
    bottom: 0;
    border-bottom: 2px solid var(--line);
    background: color-mix(in srgb, var(--line) 15%, transparent);
    pointer-events: none;
  }
  .error-message {
    color: #ce4141;
    max-width: 580px;
    white-space: normal;
    margin-top: 5px;
  }
  .error-message button {
    margin-left: 8px;
  }
  .incomplete {
    position: absolute;
    right: 8px;
    bottom: 0;
    font-size: 10px;
    color: #c57822;
  }
</style>
