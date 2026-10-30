<script lang="ts">
  import { withResourceLanes } from '../spellClassification';
  import type { TimelineEvent } from '../models/TimelineEvent';
  import Alert from './Alert.svelte';
  import Spinner from './Spinner.svelte';
  import { getBlizzardClassColor, getBlizzardClassColors } from '../classColors';
  import { getSpecIconUrl } from '../wowAssets';
  import { timelineErrorSummary } from '../timelineError';
  import TimelinePhases from './TimelinePhases.svelte';
  import type { RankingReport } from '../models/RankingReport';
  import type { ReportTimeline } from '../models/ReportTimeline';
  import type { InspectedSpell } from '../models/InspectedSpell';
  import type { TimelineMagnification } from '../models/TimelineMagnification';
  import {
    getTopPercentLabel,
    getRankingReportLink,
    getRotationAnalyzerLink
  } from '../rankingUtils';
  import {
    TIMELINE_ZOOM_LEVELS,
    canLoadTimeline,
    castIntervals,
    fightDuration,
    isPowerWindow,
    spellAppearance,
    spellIconUrl,
    timelineEventDetail,
    resourceEventColor,
    timelineEventTop,
    timelineTime,
    visibleTimelineEvents,
    isResourceHit
  } from '../timelineUtils';
  type Props = {
    mergedResources?: boolean;
    compositionMode?: boolean;
    survivalMode?: boolean;
    resourceMode?: 'health' | 'mana';
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
    showDirectHeals?: boolean;
    showHots?: boolean;
    showDamageTaken?: boolean;
    showManaSpent?: boolean;
    showManaReceived?: boolean;
    showReceivedBuffs?: boolean;
    loading: boolean;
    error?: string;
    inspected?: InspectedSpell | null;
    magnifier?: TimelineMagnification | null;
    onload: (_report: RankingReport) => void;
    oncopy: (_timeline: ReportTimeline) => void;
    onreference?: (_report: RankingReport) => void;
    isReference?: boolean;
  };
  let {
    mergedResources = false,
    compositionMode = false,
    survivalMode = false,
    resourceMode = 'health',
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
    showDirectHeals = false,
    showHots = false,
    showDamageTaken = false,
    showManaSpent = false,
    showManaReceived = false,
    showReceivedBuffs = false,
    loading,
    error,
    inspected = $bindable(null),
    magnifier = $bindable(null),
    onload,
    oncopy,
    onreference,
    isReference = false
  }: Props = $props();
  const faction = $derived(
    report.players?.[0]?.factionId === 1
      ? 'Alliance'
      : report.players?.[0]?.factionId === 2
        ? 'Horde'
        : null
  );
  const endTime = $derived(fightDuration(report, timeline));
  const estimatedHealthRange = $derived.by(() => {
    const values = (timeline?.healthCapacities ?? []).flatMap((sample) =>
      sample.estimatedMaxHealth != null ? [sample.estimatedMaxHealth] : []
    );
    if (values.length === 0 && timeline?.estimatedMaxHealth != null)
      values.push(timeline.estimatedMaxHealth);
    if (values.length === 0) return null;
    return { min: Math.min(...values), max: Math.max(...values) };
  });
  const playerClass = $derived(
    Object.keys(getBlizzardClassColors()).find((slug) =>
      report.players?.[0]?.specSlug.endsWith(`-${slug}`)
    )
  );
  const playerSpec = $derived(
    playerClass ? report.players?.[0]?.specSlug.slice(0, -(playerClass.length + 1)) : ''
  );
  const endLabel = $derived(
    `${report.fights?.[0]?.isKill ? 'Kill' : 'Fight end'} · ${timelineTime(endTime)}`
  );
  const events = $derived(
    timeline
      ? laneEvents(
          visibleTimelineEvents(timeline, hidden, {
            showCasts,
            showBuffs,
            showDeaths,
            showDirectHeals,
            showHots,
            showDamageTaken,
            showManaSpent,
            showManaReceived
          })
        ).map((event) => ({ event }))
      : []
  );
  function laneEvents(events: TimelineEvent[]) {
    return mergedResources
      ? withResourceLanes(
          events,
          timeline?.spells ?? [],
          timeline?.events ?? [],
          showReceivedBuffs,
          timeline?.playerName
        )
      : events;
  }
  const gaps = $derived(
    inspected
      ? castIntervals(
          events.map(({ event }) => event),
          inspected.id
        )
      : []
  );
  const baseHeight = $derived(
    mergedResources && events.some(({ event }) => event.displayBelow) ? 108 : 68
  );
  const height = $derived(baseHeight);
  function updateMagnifier(mouse: MouseEvent) {
    const track =
      mouse.currentTarget instanceof HTMLElement ? mouse.currentTarget.closest('.track') : null;
    if (!timeline || !track || zoom === TIMELINE_ZOOM_LEVELS.at(-1)) {
      magnifier = null;
      return;
    }
    const rect = track.getBoundingClientRect();
    const timestamp = Math.max(
      0,
      Math.min(
        timeline.duration,
        ((mouse.clientX - rect.left) / Math.max(1, rect.width)) * axisDuration
      )
    );
    const previewWidth = Math.min(360, window.innerWidth - 16) - 18;
    const previewZoom = TIMELINE_ZOOM_LEVELS[TIMELINE_ZOOM_LEVELS.length - 1];
    const span = Math.min(
      timeline.duration,
      (previewWidth * axisDuration) / Math.max(1, rect.width * (previewZoom / zoom))
    );
    const eventElement =
      mouse.target instanceof Element ? mouse.target.closest('[data-event-index]') : null;
    const hoveredEvent = eventElement
      ? events[Number(eventElement.getAttribute('data-event-index'))]?.event
      : undefined;
    magnifier = {
      timeline,
      events: events.map(({ event }) => event),
      hoveredEvent,
      mergedResources,
      showIcons,
      previewPixelsPerMillisecond: previewWidth / span,
      timestamp,
      resourceMode: survivalMode ? resourceMode : undefined,
      phases:
        report.bossTimeline && report.bossTimeline.fightId === report.fights?.[0]?.fightId
          ? report.bossTimeline.phases
          : report.fights?.[0]?.phases,
      start: Math.max(0, Math.min(timestamp - span / 2, timeline.duration - span)),
      span,
      zoom: previewZoom,
      x: mouse.clientX,
      y: mouse.clientY,
      trackTop: rect.top
    };
  }
  function reportDate(report: RankingReport): string | null {
    const date = new Date(report.fights?.[0]?.startTime || report.startTime);
    return Number.isFinite(date.getTime()) ? date.toLocaleDateString() : null;
  }
</script>

<div
  class="player-row"
  class:survival-row={survivalMode}
  class:mana-row={survivalMode && resourceMode === 'mana'}
  class:health-row={survivalMode && resourceMode === 'health'}
  style={`min-height: ${height}px`}
>
  {#if !compositionMode}<div
      class="rank"
      aria-label={survivalMode ? undefined : `Position ${rank}`}
    >
      {survivalMode ? '' : rank}
    </div>{/if}
  <div class="identity">
    {#if survivalMode}
      {#if resourceMode === 'health'}
        <div class="player-heading">Health</div>
        <div
          class="player-meta health-capacity"
          title={timeline?.maxHealth == null && estimatedHealthRange != null
            ? 'Minimum and maximum estimated HP from qualifying healing amounts and recorded percentage gains. Rounding can affect these estimates.'
            : 'Maximum HP'}
        >
          {timeline?.maxHealth != null
            ? timeline.maxHealth.toLocaleString(undefined, { maximumFractionDigits: 2 })
            : estimatedHealthRange != null
              ? estimatedHealthRange.min !== estimatedHealthRange.max
                ? `~${estimatedHealthRange.min.toLocaleString(undefined, { maximumFractionDigits: 2 })} - ~${estimatedHealthRange.max.toLocaleString(undefined, { maximumFractionDigits: 2 })}`
                : `~${estimatedHealthRange.min.toLocaleString(undefined, { maximumFractionDigits: 2 })}`
              : 'Unavailable'}
        </div>
      {:else}
        <div class="player-heading">{timeline?.playerName || report.players?.[0]?.name}</div>
        <div class="player-meta">Mana · abilities & consumables</div>
      {/if}
    {:else}
      <div class="player-heading">
        <a
          class="player-report-link"
          href={getRankingReportLink(report, edition)}
          target="_blank"
          rel="noopener"
          title="Open this player's fight in Warcraft Logs in a new tab."
        >
          {#if playerClass && playerSpec}
            <img
              class="player-spec-icon"
              src={getSpecIconUrl(playerClass, playerSpec)}
              alt=""
              title={report.players?.[0]?.specSlug}
            />
          {/if}
          <span style:color={playerClass ? getBlizzardClassColor(playerClass) : undefined}
            >{timeline?.playerName || report.players?.[0]?.name || 'Unknown player'}</span
          >
          <svg
            width="11"
            height="11"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            aria-hidden="true"
          >
            <path
              d="M14 3h7v7m0-7L10 14M10 3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-5"
            />
          </svg>
        </a>
        <span class="player-performance">
          {#if compositionMode && report.players?.[0]?.parsePercentile == null}
            Parse unavailable
          {:else}
            {(report.players?.[0]?.performance ?? 0).toLocaleString(undefined, {
              maximumFractionDigits: 0
            })} ({getTopPercentLabel(
              compositionMode ? report.players?.[0]?.parsePercentile : report.percentile
            )})
          {/if}
        </span>
      </div>
      <div class="player-details">
        <span class="player-meta player-guild" title={report.players?.[0]?.guildName || ''}>
          {#if faction}
            <img
              class="faction-icon"
              src={`/images/wow/shared/factions/${faction.toLowerCase()}.png`}
              alt={faction}
              title={faction}
            />
          {/if}
          {#if report.players?.[0]?.guildName}
            <span
              class="guild-name"
              class:alliance={faction === 'Alliance'}
              class:horde={faction === 'Horde'}>{report.players[0].guildName}</span
            >
          {/if}
          <span>{report.players?.[0]?.serverRegion || '—'}</span>
        </span>
        <span class="player-actions">
          {#if reportDate(report)}<span class="player-meta report-time">{reportDate(report)}</span
            >{/if}
          {#if getRotationAnalyzerLink(report)}
            {#if onreference}
              <button
                type="button"
                class="reference-target copy-note"
                aria-label="Use as boss reference"
                aria-pressed={isReference}
                disabled={isReference}
                title={isReference
                  ? 'Current boss reference'
                  : 'Use this fight as the boss reference.'}
                onclick={() => onreference?.(report)}
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
                  <circle cx="12" cy="12" r="7" />
                  <circle cx="12" cy="12" r="2" />
                  <path d="M12 2v3m0 14v3M2 12h3m14 0h3" />
                </svg>
              </button>
            {/if}
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
    {/if}
  </div>
  <div
    class="track"
    role="group"
    aria-label={`${timeline?.playerName ?? report.players?.[0]?.name ?? 'Player'} ${survivalMode ? resourceMode : 'ability'} timeline`}
    style={`height: ${height}px`}
    onmousemove={updateMagnifier}
    onblur={() => {
      magnifier = null;
      inspected = null;
    }}
    onmouseout={(mouse) => {
      if (
        !(mouse.relatedTarget instanceof Node) ||
        !mouse.currentTarget.contains(mouse.relatedTarget)
      ) {
        magnifier = null;
        inspected = null;
      }
    }}
  >
    {#if survivalMode && resourceMode === 'mana'}
      <span class="resource-capacity"
        >Max mana: {timeline?.maxMana?.toLocaleString(undefined, { maximumFractionDigits: 2 }) ??
          'Unavailable'}</span
      >
    {/if}
    <TimelinePhases
      phases={report.bossTimeline && report.bossTimeline.fightId === report.fights?.[0]?.fightId
        ? report.bossTimeline.phases
        : (report.fights?.[0]?.phases ?? [])}
      {axisDuration}
    />
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
        aria-label={endLabel}><span class="fight-end-label">{timelineTime(endTime)}</span></span
      >
    {/if}
    {#if timeline}
      {#if magnifier?.timeline.reportCode === timeline.reportCode && magnifier.timeline.fightId === timeline.fightId && magnifier.timeline.sourceId === timeline.sourceId && magnifier.resourceMode === (survivalMode ? resourceMode : undefined)}
        <span
          class="magnified-section"
          style={`left: ${(magnifier.start / axisDuration) * 100}%; width: ${(magnifier.span / axisDuration) * 100}%`}
          aria-hidden="true"
        ></span>
      {/if}
      {#each gaps as gap (gap.start)}
        <span
          class="cast-interval"
          style={`left: ${(gap.start / axisDuration) * 100}%; width: ${((gap.end - gap.start) / axisDuration) * 100}%; top: 36px; --spell-color: ${inspected?.color}`}
          role="img"
          aria-label={`${timeline.playerName} · ${inspected?.name} · ${gap.label} between casts at ${timelineTime(gap.start)} and ${timelineTime(gap.end)}`}
          ><span>{gap.label}</span></span
        >
      {/each}
      {#each events as { event }, index (`${event.kind}-${event.spellId}-${event.timestamp}-${index}`)}
        {@const spell = timeline.spells.find((item) => item.id === event.spellId)}
        {@const icon = spellIconUrl(spell?.icon ?? null)}
        {@const powerWindow = isPowerWindow(spell)}
        {@const appearance = spellAppearance(spell, castSpellIds.has(event.spellId))}
        {@const resourceColor = resourceEventColor(event, spell)}
        {@const title = timelineEventDetail(event, spell)}
        {#if event.kind === 'cast' || isResourceHit(event) || event.displayBelow}
          <span
            class="cast-connectors"
            class:below={event.displayBelow}
            class:instant={event.castStart == null || event.castStart === event.timestamp}
            style={`--spell-color: ${resourceColor ?? appearance.color}; left: ${((event.castStart ?? event.timestamp) / axisDuration) * 100}%; width: ${((event.timestamp - (event.castStart ?? event.timestamp)) / axisDuration) * 100}%; top: ${event.displayBelow ? 47 : timelineEventTop(event)}px`}
            aria-hidden="true"
          ></span>
        {/if}
        {#if event.displayBelow && event.kind === 'buff'}
          <span
            class="cast-window"
            style={`--spell-color: ${resourceColor ?? appearance.color}; left: ${(event.timestamp / axisDuration) * 100}%; width: ${((event.duration ?? 0) / axisDuration) * 100}%; top: 47px`}
            aria-label={title}
          ></span>
        {/if}
        {#if event.kind === 'cast' && event.castStart != null}
          <span
            class="cast-window"
            style={`--spell-color: ${resourceColor ?? appearance.color}; left: ${(event.castStart / axisDuration) * 100}%; width: ${((event.timestamp - event.castStart) / axisDuration) * 100}%; top: 47px`}
            aria-label={title}
          ></span>
        {/if}
        <button
          type="button"
          class="event"
          data-event-index={index}
          class:buff={event.kind === 'buff' && !event.displayBelow}
          class:below={event.displayBelow}
          class:death={event.kind === 'death'}
          class:cast={event.kind === 'cast' || event.kind === 'marker'}
          class:cast-icon={event.kind === 'cast' || isResourceHit(event) || event.displayBelow}
          class:healing={event.kind === 'heal' || event.kind === 'hot'}
          class:damage-taken={event.kind === 'damage'}
          class:mana-event={event.kind === 'mana-spent' || event.kind === 'mana-returned'}
          class:power-window={powerWindow}
          class:spell-dimmed={inspected !== null && inspected.id !== event.spellId}
          class:spell-highlighted={inspected?.id === event.spellId}
          style={`--spell-color: ${resourceColor ?? appearance.color}; left: ${(event.timestamp / axisDuration) * 100}%; top: ${timelineEventTop(event)}px; width: ${event.kind === 'buff' && !event.displayBelow ? `max(2px, ${((event.duration ?? 0) / axisDuration) * 100}%)` : event.kind === 'death' ? '2px' : '22px'}`}
          onmouseenter={(mouse) => {
            updateMagnifier(mouse);
            inspected = {
              id: event.spellId,
              name: spell?.name ?? 'Death',
              detail: `${timeline.playerName} · ${title}`,
              color: resourceColor ?? appearance.color
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
              detail: `${timeline.playerName} · ${title}`,
              color: resourceColor ?? appearance.color
            })}
          onblur={() => (inspected = null)}
          aria-label={title}
        >
          {#if resourceColor && (event.kind !== 'buff' || event.displayBelow)}
            {#if showIcons && icon}<img src={icon} alt="" loading="lazy" />
            {:else}<span class="cast-dot">{spell?.name ?? `Spell ${event.spellId}`}</span>{/if}
          {:else if event.kind === 'cast'}
            {#if showIcons && icon}<img src={icon} alt="" loading="lazy" />
            {:else}<span class="cast-dot">◆</span>{/if}
          {:else if event.kind === 'marker'}<span class="cast-dot" aria-hidden="true">◇</span>
          {:else if event.kind === 'death'}<span class="death-label">†</span>{/if}
        </button>
      {/each}
      {#if !timeline.isComplete}<div class="incomplete">
          <Alert variant="warning" compact>Partial timeline — event limit reached</Alert>
        </div>{/if}
      {#if timeline.events.length === 0}
        <div class="track-message">
          <Alert compact
            >Warcraft Logs returned no cast, buff or death events for this player and fight.</Alert
          >
        </div>
      {:else if events.length === 0}<div class="track-message selection-message">
          <Alert compact>No events match the spell / timeline selection.</Alert>
        </div>{/if}
    {:else if loading}
      <span class="track-message loading-message" role="status"
        ><Spinner inline />Loading report events…</span
      >
    {:else if error}
      <div class="track-message error-message">
        <Alert variant="error" compact>
          <span>{timelineErrorSummary(error)}</span>
          <button type="button" onclick={() => onload(report)}>Retry</button>
        </Alert>
      </div>
    {:else if canLoadTimeline(report)}
      <div class="unloaded-controls">
        <button class="load-row" type="button" onclick={() => onload(report)}>
          Load report {report.title || report.reportId}
        </button>
      </div>
    {:else}
      <div class="track-message">
        <Alert compact>Timeline unavailable: report is missing fight or player identity.</Alert>
      </div>
    {/if}
    {#if timeline}
      <button
        class="copy-note timeline-copy"
        style={`left: calc(${(endTime / axisDuration) * 100}% + 4px)`}
        type="button"
        aria-label="Copy note"
        title="Preview and copy this player's selected casts as an MRT or NSRT raid note."
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
  .mana-row {
    --resource-color: #79b4ff;
  }
  .resource-capacity {
    position: absolute;
    top: 0;
    left: 3px;
    color: var(--resource-color);
    font-size: 10px;
  }
  .mana-event {
    color: #69aaff;
    background: #203d68;
  }
  .survival-row {
    border-top: 1px dashed var(--line);
  }
  .event.healing,
  .event.damage-taken,
  .event.mana-event {
    height: 22px;
    color: var(--spell-color);
    background: transparent;
  }
  .event.healing img,
  .event.damage-taken img,
  .event.mana-event img {
    width: 22px;
    height: 22px;
    border: 2px solid var(--spell-color);
    border-radius: 2px;
  }
  .damage-taken {
    color: #ff6969;
    background: #612b2b;
  }
  .healing {
    color: #69d98b;
    background: #245c35;
  }
  .reference-target[aria-pressed='true'] {
    color: var(--panel);
    border-color: var(--accent);
    background: var(--accent);
    opacity: 1;
    cursor: default;
  }
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
  @media (max-width: 600px) {
    .player-row {
      flex-direction: column;
    }
    .player-row .identity {
      box-sizing: border-box;
      width: var(--timeline-viewport-width);
      border-right: 0;
      border-bottom: 1px solid var(--line);
    }
    .player-row .track {
      width: calc(100% - 34px);
    }
    .player-row .copy-note {
      min-width: 30px;
      min-height: 30px;
      box-sizing: border-box;
    }
    .player-row .player-heading {
      min-height: 24px;
    }
  }
  .identity {
    display: flex;
    flex-direction: column;
    justify-content: center;
    width: var(--timeline-identity-width, 220px);
    flex-shrink: 0;
    padding: 6px 8px;
    position: sticky;
    left: var(--timeline-position-width, 0px);
    background: var(--panel);
    border-right: 1px solid var(--line);
    z-index: 3;
  }
  .track {
    position: relative;
    width: calc(
      100% - var(--timeline-identity-width, 220px) - 28px - var(--timeline-position-width, 0px)
    );
    flex-shrink: 0;
  }
  .track {
    overflow: visible;
  }
  .player-row > .rank {
    width: var(--timeline-position-width, 32px);
    flex-shrink: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    position: sticky;
    left: 0;
    z-index: 4;
    background: var(--panel);
    border-right: 1px solid var(--line);
    box-sizing: border-box;
    text-align: center;
    font-weight: 700;
    color: var(--text);
    font-variant-numeric: tabular-nums;
  }
  .player-heading {
    display: flex;
    width: max-content;
    min-width: 100%;
    align-items: baseline;
    gap: 5px;
    font-size: 13px;
    font-weight: 600;
  }
  .player-spec-icon {
    width: 18px;
    height: 18px;
    flex-shrink: 0;
    border-radius: 3px;
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
    width: max-content;
    min-width: 100%;
    align-items: center;
    justify-content: space-between;
    gap: 6px;
  }
  .player-details .player-meta {
    margin-top: 0;
  }
  .player-guild {
    min-width: 0;
    display: inline-flex;
    align-items: center;
    gap: 4px;
    overflow: visible;
    white-space: nowrap;
  }
  .player-guild > span:last-child {
    flex-shrink: 0;
    white-space: nowrap;
  }
  .guild-name {
    font-weight: 700;
  }
  .guild-name.alliance {
    color: #69aaff;
  }
  .guild-name.horde {
    color: #ff6969;
  }
  .faction-icon {
    width: 14px;
    height: 14px;
    object-fit: contain;
    flex-shrink: 0;
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
    transform: translateY(-50%);
    z-index: 1;
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
    border: 1px solid var(--resource-color, var(--text));
    background: color-mix(in srgb, var(--resource-color, var(--text)) 18%, transparent);
    pointer-events: none;
    z-index: 4;
  }
  .cast-window {
    position: absolute;
    height: 8px;
    border: 1px solid var(--spell-color);
    background: color-mix(in srgb, var(--spell-color) 25%, transparent);
    pointer-events: none;
  }
  .cast-icon {
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
  .event.below {
    height: 22px;
    border: 1px solid var(--spell-color);
    background: var(--panel);
    z-index: 2;
  }
  .event.below img {
    width: 22px;
    height: 22px;
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
    transform: translateY(calc(4px - 50%));
    height: 8px;
    background: var(--spell-color);
    opacity: 0.5;
    border-radius: 2px;
  }
  .death {
    background: #e35d5d;
    height: 52px;
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
    z-index: 1;
    max-width: calc(100% - 16px);
  }
  .loading-message,
  .selection-message,
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
    background: color-mix(in srgb, var(--line) 15%, transparent);
    pointer-events: none;
  }
  .error-message {
    display: flex;
    align-items: center;
    gap: 8px;
    max-width: 580px;
    white-space: normal;
    margin-top: 5px;
  }
  .incomplete {
    position: absolute;
    right: 8px;
    bottom: 0;
    font-size: 10px;
  }
</style>
