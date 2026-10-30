<script lang="ts">
  import type { InspectedSpell } from '../models/InspectedSpell';
  import type { TimelineMagnification } from '../models/TimelineMagnification';
  import {
    visibleTimelineEvents,
    spellAppearance,
    spellIconUrl,
    timelineTime,
    preciseTimelineTime,
    timelineEventDetail,
    timelineEventDetails,
    resourceEventColor,
    isResourceHit,
    isChanneledSpell
  } from '../timelineUtils';
  type Props = {
    magnifier: TimelineMagnification | null;
    inspected: InspectedSpell | null;
    hidden: Set<number>;
    castSpellIds: Set<number>;
    showCasts: boolean;
    showBuffs: boolean;
    showDeaths: boolean;
    showDirectHeals?: boolean;
    showHots?: boolean;
    showDamageTaken?: boolean;
    showManaSpent?: boolean;
    showManaReceived?: boolean;
  };
  let {
    magnifier,
    hidden,
    castSpellIds,
    showCasts,
    showBuffs,
    showDeaths,
    showDirectHeals = false,
    showHots = false,
    showDamageTaken = false,
    showManaSpent = false,
    showManaReceived = false
  }: Props = $props();
  let modalWidth = $state(360);
  let modalHeight = $state(0);
  let viewportWidth = $state(1024);
  let viewportHeight = $state(768);
  const visibleEvents = $derived(
    magnifier
      ? (magnifier.events ??
          visibleTimelineEvents(
            magnifier.timeline,
            magnifier.resourceMode ? new Set() : hidden,
            magnifier.resourceMode ? true : showCasts,
            magnifier.resourceMode ? true : showBuffs,
            magnifier.resourceMode === 'mana' ? false : showDeaths,
            magnifier.resourceMode === 'health' && showDirectHeals,
            magnifier.resourceMode === 'health' && showHots,
            magnifier.resourceMode === 'health' && showDamageTaken,
            magnifier.resourceMode === 'mana' && showManaSpent,
            magnifier.resourceMode === 'mana' && showManaReceived
          ))
      : []
  );
  const activeEvents = $derived(
    visibleEvents.filter((event) => {
      if (magnifier?.timestamp == null) return true;
      const time = magnifier.timestamp;
      if (event.kind !== 'cast' && magnifier.hoveredEvent?.kind === event.kind &&
          magnifier.hoveredEvent.spellId === event.spellId &&
          magnifier.hoveredEvent.timestamp === event.timestamp) return true;
      if (isResourceHit(event)) {
        const hovered = magnifier.hoveredEvent;
        return (hovered?.kind === event.kind && hovered.spellId === event.spellId &&
          hovered.timestamp === event.timestamp) ||
          (time <= event.timestamp && time >= event.timestamp -
            (magnifier.previewPixelsPerMillisecond ? 22 / magnifier.previewPixelsPerMillisecond : 0));
      }
      if (event.kind === 'cast') {
        const hovered = magnifier.hoveredEvent;
        if (
          (event.castStart == null || event.castStart === event.timestamp) &&
          hovered?.kind === event.kind &&
          hovered.spellId === event.spellId &&
          hovered.timestamp === event.timestamp
        )
          return true;
        return event.castStart != null && event.castStart < event.timestamp
          ? event.castStart <= time && time <= event.timestamp
          : time <= event.timestamp &&
              time >=
                event.timestamp -
                  (magnifier.previewPixelsPerMillisecond
                    ? 22 / magnifier.previewPixelsPerMillisecond
                    : 0);
      }
      const duration =
        event.kind === 'buff'
          ? (event.duration ?? 0)
          : magnifier.previewPixelsPerMillisecond
            ? (event.kind === 'death' ? 2 : 22) / magnifier.previewPixelsPerMillisecond
            : 0;
      return (
        event.timestamp <= time &&
        (event.kind === 'buff'
          ? time < event.timestamp + duration
          : time <= event.timestamp + duration)
      );
    })
  );
  const lanes = $derived(
    visibleEvents
      .map((event) => ({ event }))
      .filter(
        ({ event }) =>
          magnifier &&
          (event.castStart ?? event.timestamp) -
            ((event.kind === 'cast' || isResourceHit(event) || event.displayBelow) && magnifier.previewPixelsPerMillisecond
              ? 22 / magnifier.previewPixelsPerMillisecond
              : 0) <=
            magnifier.start + magnifier.span &&
          event.timestamp +
            (event.kind === 'buff'
              ? (event.duration ?? 0)
              : event.kind !== 'cast' && !isResourceHit(event) && magnifier.previewPixelsPerMillisecond
                ? (event.kind === 'death' ? 2 : 22) / magnifier.previewPixelsPerMillisecond
                : 0) >=
            magnifier.start
      )
  );
  const details = $derived(
    [...activeEvents]
      .sort(
        (a, b) =>
          Math.abs(a.timestamp - (magnifier?.timestamp ?? 0)) -
          Math.abs(b.timestamp - (magnifier?.timestamp ?? 0))
      )
      .sort((a, b) => Number(b.kind === 'buff') - Number(a.kind === 'buff'))
  );
</script>

<svelte:window bind:innerWidth={viewportWidth} bind:innerHeight={viewportHeight} />

{#if magnifier}
  {@const start = magnifier.start}
  {@const span = magnifier.span}
  {@const health = magnifier.timeline.healthCapacities?.findLast(
    (sample) => sample.timestamp <= (magnifier.timestamp ?? magnifier.start))}
  {@const nextHealth = magnifier.timeline.healthCapacities?.find(
    (sample) => sample.timestamp > (magnifier.timestamp ?? magnifier.start))}
  {@const nearbyEstimate = nextHealth?.estimatedMaxHealth != null &&
    nextHealth.timestamp - (magnifier.timestamp ?? magnifier.start) <= 1000
      ? nextHealth.estimatedMaxHealth : null}
  {@const maximumHealth = health?.maxHealth ?? health?.estimatedMaxHealth ?? nearbyEstimate}
  {@const estimated = health?.maxHealth == null &&
    (health?.estimatedMaxHealth != null || nearbyEstimate != null)}
  <div
    class="timeline-magnifier"
    class:health={magnifier.resourceMode === 'health'}
    class:mana={magnifier.resourceMode === 'mana'}
    role="tooltip"
    bind:offsetWidth={modalWidth}
    bind:offsetHeight={modalHeight}
    aria-label="Magnified timeline section"
    style={`left: ${Math.max(16, Math.min(magnifier.x - (modalWidth || 360) / 2, viewportWidth - (modalWidth || 360) - 16))}px; top: ${Math.max(16, Math.min((magnifier.trackTop ?? magnifier.y) - modalHeight - 8, viewportHeight - modalHeight - 16))}px`}
  >
    <div class="magnifier-heading">
      <strong>{magnifier.timeline.playerName}</strong>
      <span
        >{magnifier.resourceMode === 'health'
          ? `Health${maximumHealth != null ? ` ${estimated ? '~' : ''}${maximumHealth.toLocaleString(undefined, { maximumFractionDigits: 2 })}` : ''}`
          : magnifier.resourceMode === 'mana'
            ? 'Mana'
            : 'Timeline'}</span
      >
      <span class="magnifier-zoom">{magnifier.zoom}%</span>
    </div>
    {#if magnifier.timestamp != null}
      <div class="hover-time">At {preciseTimelineTime(magnifier.timestamp)}</div>
    {/if}
    {#if magnifier.resourceMode === 'mana'}
      <div>Max mana: {magnifier.timeline.maxMana?.toLocaleString(undefined, { maximumFractionDigits: 2 }) ?? 'Unavailable'}</div>
    {/if}
    <div class="magnified-track" style:height={magnifier.mergedResources && visibleEvents.some((event) => event.displayBelow) ? '108px' : '68px'}>
      {#each lanes as { event }, index (index)}
        {@const spell = magnifier.timeline.spells.find((item) => item.id === event.spellId)}
        {@const appearance = spellAppearance(spell, castSpellIds.has(event.spellId))}
        {@const icon = spellIconUrl(spell?.icon ?? null)}
        {@const resourceColor = resourceEventColor(event, spell)}
        {#if event.kind === 'cast' || isResourceHit(event) || event.displayBelow}
          <span
            class="cast-connectors"
            class:below={event.displayBelow}
            class:instant={event.castStart == null || event.castStart === event.timestamp}
            style={`--spell-color: ${resourceColor ?? appearance.color}; left: ${(((event.castStart ?? event.timestamp) - start) / span) * 100}%; width: ${((event.timestamp - (event.castStart ?? event.timestamp)) / span) * 100}%; top: ${event.displayBelow ? 47 : 19}px`}
            aria-hidden="true"
          ></span>
        {/if}
        {#if event.displayBelow && event.kind === 'buff'}
          <span class="cast-window"
            style={`--spell-color: ${resourceColor ?? appearance.color}; left: ${((event.timestamp - start) / span) * 100}%; width: ${((event.duration ?? 0) / span) * 100}%; top: 47px`}
            aria-label={timelineEventDetail(event, spell)}></span>
        {/if}
        {#if event.kind === 'cast' && event.castStart != null}
          <span
            class="cast-window"
            style={`--spell-color: ${resourceColor ?? appearance.color}; left: ${((event.castStart - start) / span) * 100}%; width: ${((event.timestamp - event.castStart) / span) * 100}%; top: 47px`}
            aria-label={timelineEventDetail(event, spell)}
          ></span>
        {/if}
        <span
          class="magnified-event"
          class:buff={event.kind === 'buff' && !event.displayBelow}
          class:below={event.displayBelow}
          class:cast={event.kind === 'cast' || event.kind === 'marker'}
          class:cast-icon={event.kind === 'cast' || isResourceHit(event) || event.displayBelow}
          class:death={event.kind === 'death'}
          class:healing={event.kind === 'heal' || event.kind === 'hot'}
          class:damage-taken={event.kind === 'damage'}
          class:mana-event={event.kind === 'mana-spent' || event.kind === 'mana-returned'}
          class:power-window={appearance.group === 'power'}
          aria-label={timelineEventDetail(event, spell)}
          style={`--spell-color: ${resourceColor ?? appearance.color}; left: ${((event.timestamp - start) / span) * 100}%; top: ${event.displayBelow ? 75 : event.kind === 'buff' ? 47 : 19}px; width: ${event.kind === 'buff' && !event.displayBelow ? `max(2px, ${((event.duration ?? 0) / span) * 100}%)` : event.kind === 'death' ? '2px' : '22px'}`}
          >{#if (event.kind === 'cast' || event.displayBelow || (resourceColor && event.kind !== 'buff')) && (magnifier.showIcons ?? true) && icon}<img
              src={icon}
              alt=""
            />
          {:else if event.kind === 'marker'}<span aria-label="Inferred marker">◇</span>
          {:else if resourceColor && (event.kind !== 'buff' || event.displayBelow)}{spell?.name ?? `Spell ${event.spellId}`}
          {:else if event.kind === 'death'}†
          {:else if event.kind === 'cast'}◆{/if}</span
        >
      {/each}
      {#each magnifier.phases ?? [] as phase, index (index)}
        {#if phase.timestamp >= start && phase.timestamp <= start + span}
          <span class="preview-phase" style:left={`${((phase.timestamp - start) / span) * 100}%`}
            >P{phase.id}</span
          >
        {/if}
      {/each}
      {#if magnifier.timestamp != null}
        <span class="preview-cursor" style:left={`${((magnifier.timestamp - start) / span) * 100}%`}
        ></span>
      {/if}
    </div>
    <div class="magnifier-times">
      <span>{timelineTime(start)}</span><span>{timelineTime(start + span)}</span>
    </div>
    <ul class="event-details">
      {#each details as event, index (index)}
        {@const healthEvent = event.kind === 'heal' || event.kind === 'hot' || event.kind === 'damage'}
        {@const spell = magnifier.timeline.spells.find((spell) => spell.id === event.spellId)}
        {@const parts = timelineEventDetails({ ...event, sourceName: null }, spell, false)}
        {@const manaCost = event.kind === 'cast'
          ? magnifier.timeline.events.filter((cost) =>
              cost.kind === 'mana-spent' && !cost.isManaDrain && cost.spellId === event.spellId &&
              cost.timestamp === event.timestamp &&
              (!cost.sourceName || cost.sourceName === magnifier.timeline.playerName))
            .reduce((total, cost) => total + Math.max(0, cost.amount ?? 0), 0)
          : 0}
        {@const casting =
          event.kind === 'cast' &&
          event.castStart != null &&
          event.castStart < event.timestamp &&
          magnifier.timestamp != null &&
          event.castStart <= magnifier.timestamp &&
          magnifier.timestamp < event.timestamp}
        {@const channeling = event.kind === 'buff' && isChanneledSpell(spell) &&
          magnifier.timestamp != null && event.timestamp <= magnifier.timestamp &&
          magnifier.timestamp < event.timestamp + (event.duration ?? 0)}
        <li
          class:ability-group={event.kind !== 'buff' &&
            (index === 0 || details[index - 1].kind === 'buff')}
        >
          {#if index === 0 || (event.kind !== 'buff' && details[index - 1].kind === 'buff')}
            <strong class="detail-group-label"
              >{event.kind === 'buff' ? 'Buffs' : 'Abilities'}</strong
            >
          {/if}
          <div class="detail-row" class:health-detail-line={healthEvent}>
            <span class="detail-time">
              {channeling
                ? `${preciseTimelineTime(event.timestamp)} - ${preciseTimelineTime(event.timestamp + (event.duration ?? 0))} (${Number(((event.duration ?? 0) / 1000).toFixed(2))}s)`
                : casting
                ? `${preciseTimelineTime(event.castStart ?? event.timestamp)} - ${preciseTimelineTime(event.timestamp)} (${Number(((event.timestamp - (event.castStart ?? event.timestamp)) / 1000).toFixed(2))}s)`
                : parts[0]}
            </span>
            <span class="detail-name">
              {parts[1]}{#if event.sourceName}<span class="health-source">{` (${event.sourceName})`}</span>{/if}
              {#if manaCost > 0}<span class="mana-cost mana-loss">{` -${manaCost.toLocaleString(undefined, { maximumFractionDigits: 2 })}`}</span>{/if}
            </span>
            <span class="detail-result">
          {#if healthEvent}
            {@const change = event.healthBeforePercent != null && event.healthAfterPercent != null
              ? Number((event.healthAfterPercent - event.healthBeforePercent).toFixed(2))
              : null}
              <span class="health-result"
                title={event.healthBeforeIsPreviousSnapshot
                  ? 'Percentage change since the last recorded health snapshot before this event.'
                  : undefined}
              >
                {#if event.amount != null}
                  <span class:health-gain={event.kind !== 'damage'} class:health-loss={event.kind === 'damage'}>{parts[2]}</span>
                {/if}
                {#if change != null}
                  <span class:health-gain={change >= 0} class:health-loss={change < 0}>
                    ({change >= 0 ? '+' : ''}{change}%)
                  </span>
                {/if}
              </span>
          {:else if event.kind === 'mana-spent' || event.kind === 'mana-returned'}
            {#if event.amount != null && Number(event.amount.toFixed(2)) > 0}
              <span class="mana-cost" class:mana-loss={event.kind === 'mana-spent'}>{event.kind === 'mana-spent' ? '-' : '+'}{event.amount.toLocaleString(undefined, { maximumFractionDigits: 2 })}</span>
            {/if}
          {:else}
          {#each parts.slice(2) as detail, detailIndex (detailIndex)}
            {#if detail !== 'Cast' && detail !== 'Buff'}
              <span>{`${detail} `}</span>
            {/if}
          {/each}
          {/if}
          {#if event.kind === 'cast'}
            <span class="cast-results">
              {#each event.castResults?.filter((result) => Number(result.amount.toFixed(2)) > 0) ?? [] as result (`${result.targetId}-${result.kind}`)}
                <span class:health-gain={result.kind === 'heal'} class:health-loss={result.kind === 'damage'}>
                  {result.kind === 'heal' ? '+' : '-'}{result.amount.toLocaleString(undefined, { maximumFractionDigits: 2 })} to {result.targetName}
                </span>
              {/each}
            </span>
          {/if}
            </span>
          </div>
        </li>
      {/each}
    </ul>
  </div>
{/if}

<style>
  .detail-row {
    display: grid;
    grid-template-columns: subgrid;
    grid-column: 1 / -1;
    gap: 8px;
    width: 100%;
    align-items: start;
    line-height: 1.6;
    white-space: nowrap;
  }
  .detail-time {
    font-variant-numeric: tabular-nums;
  }
  .detail-result {
    text-align: right;
    display: flex;
    justify-content: flex-end;
    gap: 8px;
  }
  .detail-result > span {
    display: block;
  }
  .detail-result > .cast-results {
    display: flex;
    flex-direction: column;
    align-items: flex-end;
  }
  .health-result {
    display: inline-flex;
    gap: 6px;
    white-space: nowrap;
  }
  .health-source {
    color: var(--muted);
  }
  .mana-cost {
    color: #69aaff;
  }
  .mana-loss {
    color: #d9ad69;
  }
  .health-gain {
    color: #69d98b;
  }
  .health-loss {
    color: #ff6969;
  }
  .timeline-magnifier {
    position: fixed;
    box-sizing: border-box;
    width: max-content;
    min-width: min(360px, calc(100vw - 32px));
    max-width: calc(100vw - 32px);
    max-height: calc(100vh - 32px);
    overflow: auto;
    padding: 8px;
    border: 1px solid var(--text);
    border-radius: 5px;
    background: var(--panel);
    color: var(--text);
    box-shadow: 0 4px 18px #0008;
    z-index: 100;
    pointer-events: none;
    font-size: 11px;
  }
  .event-details {
    display: grid;
    grid-template-columns: max-content minmax(max-content, 1fr) max-content;
    column-gap: 8px;
    overflow-x: auto;
    padding: 0;
    list-style: none;
    margin: 6px 0 0;
  }
  .magnifier-heading {
    display: flex;
    align-items: center;
    gap: 8px;
  }
  .detail-group-label {
    grid-column: 1 / -1;
    display: block;
    margin-bottom: 2px;
  }
  .event-details .ability-group {
    margin-top: 8px;
  }
  .event-details li {
    display: grid;
    grid-template-columns: subgrid;
    grid-column: 1 / -1;
    margin-top: 4px;
  }
  .magnifier-zoom {
    margin-left: auto;
  }
  .hover-time {
    font-variant-numeric: tabular-nums;
  }
  .preview-cursor,
  .preview-phase {
    position: absolute;
    top: 0;
    height: 100%;
    border-left: 1px solid currentColor;
    z-index: 3;
  }
  .preview-phase {
    opacity: 0.6;
    font-size: 9px;
  }
  .healing,
  .damage-taken {
    color: #ef8585;
    background: #ef8585;
    height: 22px;
  }
  .mana-event {
    color: #79b4ff;
    background: #79b4ff;
    height: 22px;
  }
  .magnified-event.healing,
  .magnified-event.damage-taken,
  .magnified-event.mana-event {
    color: var(--spell-color);
    background: transparent;
  }
  .healing img,
  .damage-taken img,
  .mana-event img {
    border: 2px solid var(--spell-color);
    border-radius: 2px;
  }
  .magnified-track {
    position: relative;
    overflow: hidden;
    margin-top: 6px;
    background: var(--line);
  }
  .magnified-event {
    position: absolute;
    z-index: 1;
    border-radius: 2px;
  }
  .buff.power-window {
    height: 12px;
    border: 1px solid var(--spell-color);
    opacity: 0.9;
    z-index: 3;
  }
  .cast.power-window img {
    border: 2px solid var(--spell-color);
  }
  .cast-window {
    position: absolute;
    height: 8px;
    border: 1px solid var(--spell-color);
    border-radius: 2px;
    background: color-mix(in srgb, var(--spell-color) 25%, transparent);
  }
  .cast-icon {
    transform: translateX(-100%);
  }
  .cast-connectors {
    position: absolute;
    height: 36px;
    border-left: 1px solid var(--spell-color);
    border-right: 1px solid var(--spell-color);
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
  .magnified-event.below {
    height: 22px;
    border: 1px solid var(--spell-color);
    background: var(--panel);
    z-index: 2;
  }
  .magnified-event.below img {
    width: 22px;
    height: 22px;
  }
  .magnified-event img {
    width: 100%;
    height: 22px;
    border: 1px solid var(--spell-color);
  }
  .magnifier-times {
    display: flex;
    justify-content: space-between;
    font: 10px monospace;
    margin-top: 4px;
  }
  .cast {
    height: 22px;
    z-index: 2;
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
    height: 40px;
    z-index: 2;
  }
</style>
