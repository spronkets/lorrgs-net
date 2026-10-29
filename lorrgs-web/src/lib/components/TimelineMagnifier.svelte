<script lang="ts">
  import type { InspectedSpell } from '../models/InspectedSpell';
  import type { TimelineMagnification } from '../models/TimelineMagnification';
  import {
    visibleTimelineEvents,
    spellAppearance,
    spellIconUrl,
    timelineTime
  } from '../timelineUtils';
  type Props = {
    magnifier: TimelineMagnification | null;
    inspected: InspectedSpell | null;
    hidden: Set<number>;
    castSpellIds: Set<number>;
    showCasts: boolean;
    showBuffs: boolean;
    showDeaths: boolean;
  };
  let { magnifier, inspected, hidden, castSpellIds, showCasts, showBuffs, showDeaths }: Props =
    $props();
</script>

{#if magnifier}
  {@const start = magnifier.start}
  {@const span = magnifier.span}
  <div
    class="timeline-magnifier"
    role="tooltip"
    aria-label="Magnified timeline section"
    style={`left: clamp(8px, ${magnifier.x - 180}px, calc(100vw - 368px)); top: max(8px, ${magnifier.y - 130}px)`}
  >
    <strong>{magnifier.timeline.playerName} · {inspected?.name} · {magnifier.zoom}%</strong>
    <div class="magnified-track">
      {#each visibleTimelineEvents(magnifier.timeline, hidden, showCasts, showBuffs, showDeaths).filter((event) => event.timestamp <= start + span && event.timestamp + (event.duration ?? 0) >= start) as event, index (index)}
        {@const spell = magnifier.timeline.spells.find((item) => item.id === event.spellId)}
        {@const appearance = spellAppearance(spell, castSpellIds.has(event.spellId))}
        {@const icon = spellIconUrl(spell?.icon ?? null)}
        <span
          class="magnified-event"
          class:buff={event.kind === 'buff'}
          class:cast={event.kind === 'cast' || event.kind === 'marker'}
          class:death={event.kind === 'death'}
          class:spell-dimmed={inspected?.id !== event.spellId}
          style={`--spell-color: ${appearance.color}; left: ${((Math.max(start, event.timestamp) - start) / span) * 100}%; top: ${event.kind === 'buff' ? 34 : 4}px; width: ${event.kind === 'buff' ? `${((Math.min(start + span, event.timestamp + (event.duration ?? 0)) - Math.max(start, event.timestamp)) / span) * 100}%` : '28px'}`}
          >{#if event.kind === 'cast' && icon}<img src={icon} alt="" />
          {:else if event.kind === 'marker'}<span aria-label="Inferred marker">◇</span>{/if}</span
        >
      {/each}
    </div>
    <div class="magnifier-times">
      <span>{timelineTime(start)}</span><span>{timelineTime(start + span)}</span>
    </div>
  </div>
{/if}

<style>
  .timeline-magnifier {
    position: fixed;
    width: 360px;
    max-width: calc(100vw - 16px);
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
  .magnified-track {
    position: relative;
    overflow: hidden;
    height: 48px;
    margin-top: 6px;
    background: var(--line);
  }
  .magnified-event {
    position: absolute;
  }
  .magnified-event img {
    width: 28px;
    height: 28px;
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
</style>
