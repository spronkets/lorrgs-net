<script lang="ts">
  import { ChartNoAxesColumn, Copy, Crosshair, ExternalLink } from '@lucide/svelte';
  import { withResourceLanes } from '../spellClassification';
  import type { TimelineEvent } from '../models/TimelineEvent';
  import Alert from './Alert.svelte';

  import Spinner from './Spinner.svelte';
  import { getClassSlugForSpec, getSpecIconUrl } from '../wowAssets';
  import { timelineErrorSummary } from '../timelineError';
  import TimelinePhases from './TimelinePhases.svelte';
  import type { RankingReport } from '../models/RankingReport';
  import type { ReportTimeline } from '../models/ReportTimeline';
  import type { InspectedSpell } from '../models/InspectedSpell';
  import type { TimelineMagnification } from '../models/TimelineMagnification';
  import {
    getTopPercentLabel,
    getPlayerProfileLink,
    getRankingReportLink,
    getRotationAnalyzerLink
  } from '../rankingUtils';
  import {
    TIMELINE_ZOOM_LEVELS,
    timelinePreview,
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
  const playerClass = $derived(getClassSlugForSpec(report.players?.[0]?.specSlug));
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
      ...timelinePreview(rect, mouse.clientX, timeline.duration, axisDuration, zoom, window.innerWidth),
      resourceMode: survivalMode ? resourceMode : undefined,
      phases:
        report.bossTimeline && report.bossTimeline.fightId === report.fights?.[0]?.fightId
          ? report.bossTimeline.phases
          : report.fights?.[0]?.phases,
      x: mouse.clientX,
      y: mouse.clientY,
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
      {#snippet guildName()}
        {#if faction || report.players?.[0]?.guildName}
          <span class="guild-name faction-name"
            title={report.players?.[0]?.guildName || faction || undefined}
            class:alliance={faction === 'Alliance'} class:horde={faction === 'Horde'}
          >{report.players?.[0]?.guildName ?? ''}</span>
        {/if}
      {/snippet}
      <div class="player-heading">
        {#snippet playerName()}
          {#if playerClass && playerSpec}
            <img class="player-spec-icon" src={getSpecIconUrl(playerClass, playerSpec)}
              alt="" title={report.players?.[0]?.specSlug} />
          {/if}
          <span class={`class-name ${playerClass ?? ''}`}
            title={timeline?.playerName || report.players?.[0]?.name || 'Unknown player'}>
            {timeline?.playerName || report.players?.[0]?.name || 'Unknown player'}
          </span>
        {/snippet}
        {#if !compositionMode}
          {@const profileLink = getPlayerProfileLink(report.players?.[0], edition)}
          {#if profileLink}
            <a class="player-name" href={profileLink} target="_blank" rel="noopener noreferrer"
              title="Open this player's Warcraft Logs profile in a new tab.">
              {@render playerName()}
            </a>
          {:else}
            <span class="player-name">{@render playerName()}</span>
          {/if}
          {@render guildName()}
        {:else}
        <a
          class="player-report-link"
          href={getRankingReportLink(report, edition)}
          target="_blank"
          rel="noopener"
          title="Open this player's fight in Warcraft Logs in a new tab."
        >
            {@render playerName()}
            <ExternalLink size={11} aria-hidden="true" />
        </a>
        {/if}
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
        {#if getRotationAnalyzerLink(report) && onreference}
          <button
            class={['button', ['timeline-row-button', 'reference-target copy-note']]}
            data-tone="unstyled"
            data-outline="false"
            data-size="medium"
            type="button"
            aria-label="Use as boss reference"
            aria-pressed={isReference}
            disabled={isReference}
            title={isReference ? 'Current boss reference' : 'Use this fight as the boss reference.'}
            onclick={() => onreference?.(report)}
          >
            <Crosshair size={13} aria-hidden="true" />
          </button>
        {/if}
      </div>
      <div class="player-details">
        <span class="player-meta player-guild">
          {#if compositionMode}{@render guildName()}{/if}
          <span>{[
            report.players?.[0]?.serverName,
            report.players?.[0]?.serverRegion?.toUpperCase()
          ].filter(Boolean).join(' ')}</span>
        </span>
        <span class="player-actions">
          {#if reportDate(report)}<span class="player-meta report-time">{reportDate(report)}</span
            >{/if}
          {#if !compositionMode}
            <a class="report-icon-link copy-note"
              href={getRankingReportLink(report, edition)} target="_blank" rel="noopener noreferrer"
              aria-label="Open report in Warcraft Logs"
              title={report.title || `Report ${report.reportId}`}>
              <ExternalLink size={11} aria-hidden="true" />
            </a>
          {/if}
          {#if getRotationAnalyzerLink(report)}
            <a
              class="analyzer-link copy-note"
              href={getRotationAnalyzerLink(report)}
              aria-label="Open report analyzer"
              title="Analyze this player's fight in the report analyzer."
            >
              <ChartNoAxesColumn size={13} aria-hidden="true" />
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
        style={`left: ${Math.min(100, (endTime / axisDuration) * 100)}%; width: ${Math.max(0, (1 - endTime / axisDuration) * 100)}%`}
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
          class={[
            'button',
            [
              'timeline-row-button',
              `event`,
              {
                buff: event.kind === 'buff' && !event.displayBelow,
                below: event.displayBelow,
                death: event.kind === 'death',
                cast: event.kind === 'cast' || event.kind === 'marker',
                'cast-icon': event.kind === 'cast' || isResourceHit(event) || event.displayBelow,
                healing: event.kind === 'heal' || event.kind === 'hot',
                'damage-taken': event.kind === 'damage',
                'mana-event': event.kind === 'mana-spent' || event.kind === 'mana-returned',
                'power-window': powerWindow,
                'spell-dimmed': inspected !== null && inspected.id !== event.spellId,
                'spell-highlighted': inspected?.id === event.spellId
              }
            ]
          ]}
          data-tone="unstyled"
          data-outline="false"
          data-size="medium"
          type="button"
          data-event-index={index}
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
        <Alert variant="warning" compact>
          <span>{timelineErrorSummary(error)}</span>
          <button
            class="button"
            data-tone="warning"
            data-outline="true"
            data-size="small"
            type="button"
            onclick={() => onload(report)}>Retry</button
          >
        </Alert>
      </div>
    {:else if canLoadTimeline(report)}
      <div class="unloaded-controls">
        <button
          class={['button', ['timeline-row-button', `load-row`]]}
          data-tone="secondary"
          data-outline="true"
          data-size="small"
          type="button"
          onclick={() => onload(report)}
        >
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
        class={['button', ['timeline-row-button', `copy-note timeline-copy`]]}
        data-tone="unstyled"
        data-outline="false"
        data-size="medium"
        style={`left: calc(${(endTime / axisDuration) * 100}% + 4px)`}
        type="button"
        aria-label="Copy note"
        title="Preview and copy this player's selected casts as an MRT or NSRT raid note."
        onclick={() => oncopy(timeline)}
      >
        <Copy size={13} aria-hidden="true" />
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
  :global(.mana-event.timeline-row-button) {
    color: #69aaff;
    background: #203d68;
  }
  .survival-row {
    border-top: 1px dashed var(--line);
  }
  :global(.event.healing.timeline-row-button),
  :global(.event.damage-taken.timeline-row-button),
  :global(.event.mana-event.timeline-row-button) {
    height: 22px;
    color: var(--spell-color);
    background: transparent;
  }
  :global(.event.healing.timeline-row-button) img,
  :global(.event.damage-taken.timeline-row-button) img,
  :global(.event.mana-event.timeline-row-button) img {
    width: 22px;
    height: 22px;
    border: 2px solid var(--spell-color);
    border-radius: 2px;
  }
  :global(.damage-taken.timeline-row-button) {
    color: #ff6969;
    background: #612b2b;
  }
  :global(.healing.timeline-row-button) {
    color: #69d98b;
    background: #245c35;
  }
  :global(.reference-target[aria-pressed='true'].timeline-row-button) {
    color: var(--panel);
    border-color: var(--accent);
    background: var(--accent);
    opacity: 1;
    cursor: default;
  }
  :global(.event.spell-highlighted.timeline-row-button),
  :global(.event.timeline-row-button:focus-visible) {
    outline: 2px solid var(--text);
    outline-offset: 2px;
    opacity: 1;
    z-index: 4;
  }
  :global(.event.spell-dimmed.timeline-row-button) {
    opacity: 0.18;
  }
  :global(.event.buff.spell-highlighted.timeline-row-button) {
    min-height: 12px;
  }
  :global(.event.buff.power-window.timeline-row-button) {
    background: var(--spell-color);
    opacity: 0.9;
    height: 12px;
    z-index: 3;
    border: 1px solid var(--spell-color);
  }
  :global(.event.cast.power-window.timeline-row-button) img {
    border: 2px solid var(--spell-color);
  }
  :global(.event.power-window.timeline-row-button) .cast-dot {
    color: var(--spell-color);
  }
  :global(button.timeline-row-button:where([data-tone='unstyled'])) {
    color: var(--text);
    background: var(--panel);
    border: 1px solid var(--line);
    border-radius: 3px;
    padding: 5px 8px;
    font: inherit;
    font-size: 12px;
  }
  :global(button.timeline-row-button:where([data-tone='unstyled'])) {
    cursor: pointer;
  }
  :global(button.timeline-row-button:where([data-tone='unstyled']):disabled) {
    opacity: 0.5;
    cursor: wait;
  }
  :global(button.timeline-row-button:where([data-tone='unstyled']):hover:not(:disabled)),
  :global(button.timeline-row-button:where([data-tone='unstyled']):focus-visible) {
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
    .player-row :is(.copy-note, :global(.copy-note.timeline-row-button)) {
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
    z-index: 0;
    width: calc(
      100% - var(--timeline-identity-width, 220px) - 28px - var(--timeline-position-width, 0px)
    );
    flex-shrink: 0;
  }
  .track {
    overflow-x: clip;
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
    align-items: center;
    gap: 5px;
    font-size: 13px;
    line-height: 1.4;
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
  .player-name,
  .player-report-link {
    display: inline-flex;
    align-items: center;
    align-self: center;
    gap: 3px;
  }
  .player-name {
    min-width: 0;
    max-width: 16ch;
    overflow: hidden;
  }
  .player-name span {
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
  .report-icon-link {
    color: var(--display-value);
  }
  :global(.theme-dark) .report-icon-link {
    color: #fff;
  }
  .player-heading .guild-name {
    max-width: 24ch;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
  .player-report-link span {
    min-width: 0;
    max-width: 24ch;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
  .player-report-link :global(svg) {
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
    line-height: 1.4;
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
  .guild-name:global(.faction-name::before) {
    transform: none;
  }
  .player-performance {
    font-size: 10px;
    color: var(--muted);
  }
  :is(.copy-note, :global(.copy-note.timeline-row-button)) {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    padding: 3px;
    flex-shrink: 0;
  }
  :is(.copy-note, :global(.copy-note.timeline-row-button)) :global(svg) {
    display: block;
  }
  .player-actions {
    display: inline-flex;
    align-items: center;
    gap: 4px;
    flex-shrink: 0;
  }
  :global(.timeline-copy.timeline-row-button) {
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
  :global(.event.timeline-row-button) {
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
  :global(.cast-icon.timeline-row-button) {
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
  :global(.event.below.timeline-row-button) {
    height: 22px;
    border: 1px solid var(--spell-color);
    background: var(--panel);
    z-index: 2;
  }
  :global(.event.below.timeline-row-button) img {
    width: 22px;
    height: 22px;
  }
  :global(.cast.timeline-row-button) {
    height: 22px;
    z-index: 2;
  }
  :global(.cast.timeline-row-button) img {
    width: 22px;
    height: 22px;
    border: 1px solid var(--spell-color);
    border-radius: 2px;
  }
  .cast-dot {
    color: var(--spell-color);
  }
  :global(.buff.timeline-row-button) {
    transform: translateY(calc(4px - 50%));
    height: 8px;
    background: var(--spell-color);
    opacity: 0.5;
    border-radius: 2px;
  }
  :global(.death.timeline-row-button) {
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
  :global(.load-row.timeline-row-button) {
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
