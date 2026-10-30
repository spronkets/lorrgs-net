<script lang="ts">
  import Checkbox from './Checkbox.svelte';
  import { SvelteSet } from 'svelte/reactivity';
  import type { ReportTimeline } from '../models/ReportTimeline';
  import type { InspectedSpell } from '../models/InspectedSpell';
  import { classSpellPresets, matchesClassSpellPreset } from '../spellClassification';
  import {
    isPowerWindow,
    spellAppearance,
    spellIconUrl,
    timelineSpells,
    timelineSpellGroups
  } from '../timelineUtils';

  type Props = {
    timelines: ReportTimeline[];
    classId: number;
    edition: string;
    compositionMode?: boolean;
    hidden?: Set<number>;
    inspected?: InspectedSpell | null;
  };
  let {
    timelines,
    classId,
    edition,
    compositionMode = false,
    hidden = $bindable(new Set<number>()),
    inspected = $bindable(null)
  }: Props = $props();
  let preset = $state<string | null>('Everything');
  let search = $state('');
  let showSpellNames = $state(false);
  const spells = $derived(timelineSpells(timelines));
  const presets = $derived(classSpellPresets(classId, edition, compositionMode));
  const selectedPreset = $derived(presets.find((option) => option.key === preset));
  const castSpellIds = $derived(
    new Set(
      timelines.flatMap((timeline) =>
        timeline.events.filter((event) => event.kind === 'cast').map((event) => event.spellId)
      )
    )
  );
  const spellGroups = $derived(
    timelineSpellGroups(timelines)
      .map((group) => ({
        ...group,
        spells: group.spells.filter((spell) =>
          spell.name.toLowerCase().includes(search.trim().toLowerCase())
        )
      }))
      .filter((group) => group.spells.length)
  );
  $effect(() => {
    if (preset === 'None') {
      hidden = new SvelteSet(spells.map((spell) => spell.id));
    } else if (selectedPreset) {
      const selected = selectedPreset;
      hidden = new SvelteSet(
        spells
          .filter((spell) => !matchesClassSpellPreset(spell, selected, classId))
          .map((spell) => spell.id)
      );
    }
  });
  function toggleSpell(id: number) {
    preset = null;
    const next = new SvelteSet(hidden);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    hidden = next;
  }
  function toggleGroup(ids: number[]) {
    preset = null;
    const disable = ids.every((id) => !hidden.has(id));
    const next = new SvelteSet(hidden);
    for (const id of ids) {
      if (disable) next.add(id);
      else next.delete(id);
    }
    hidden = next;
  }
</script>

<details class="spell-selection" open>
  <summary>Spell selection · {spells.length - hidden.size} / {spells.length}</summary>
  <div class="spell-actions">
    <div class="form-field">
      <div class="input-frame">
        <input
          id="spellselection-field-1"
          class="input"
          type="text"
          aria-label="Search timeline spells"
          bind:value={search}
          placeholder="Find a spell…"
        />
      </div>
    </div>
    <Checkbox label="Spell names" bind:checked={showSpellNames} />
    <div class="spell-presets" role="group" aria-label="Spell presets">
      {#each presets as option (option.key)}
        <button
          class={['button', ['spell-selection-button']]}
          data-tone={(preset === option.key) === true || (preset === option.key) === 'true'
            ? 'primary'
            : 'secondary'}
          data-outline={!((preset === option.key) === true || (preset === option.key) === 'true')}
          data-size="small"
          type="button"
          aria-pressed={preset === option.key}
          onclick={() => (preset = preset === option.key ? 'None' : option.key)}
          ><img src={option.icon} alt="" />{option.label}</button
        >
      {/each}
    </div>
    <button
      class={['button', ['spell-selection-button']]}
      data-tone="secondary"
      data-outline="true"
      data-size="small"
      type="button"
      onclick={() => (preset = 'None')}>Clear spells</button
    >
  </div>
  <div class="spell-group-row">
    {#each spellGroups as group (group.label)}
      <div class="spell-group" role="group" aria-label={group.label}>
        <button
          class={['button', ['spell-selection-button', `group-toggle`]]}
          data-tone={group.spells.every((spell) => !hidden.has(spell.id)) === true ||
          group.spells.every((spell) => !hidden.has(spell.id)) === 'true'
            ? 'primary'
            : 'secondary'}
          data-outline={!(
            group.spells.every((spell) => !hidden.has(spell.id)) === true ||
            group.spells.every((spell) => !hidden.has(spell.id)) === 'true'
          )}
          data-size="small"
          type="button"
          aria-label={`Toggle ${group.label}`}
          aria-pressed={group.spells.every((spell) => !hidden.has(spell.id))}
          onclick={() => toggleGroup(group.spells.map((spell) => spell.id))}
          title={`Show / hide ${group.label}`}>{group.label}</button
        >
        <div class="spell-buttons">
          {#each group.spells as spell (spell.id)}
            {@const appearance = spellAppearance(spell, castSpellIds.has(spell.id))}
            <button
              class={[
                'button',
                [
                  'spell-selection-button',
                  {
                    'power-window': isPowerWindow(spell),
                    muted: hidden.has(spell.id),
                    'spell-dimmed': inspected !== null && inspected.id !== spell.id,
                    'spell-highlighted': inspected?.id === spell.id
                  }
                ]
              ]}
              data-tone="unstyled"
              data-outline="false"
              data-size="medium"
              type="button"
              style={`--spell-color: ${appearance.color}`}
              onmouseenter={() =>
                (inspected = {
                  id: spell.id,
                  name: spell.name,
                  detail: appearance.label,
                  color: appearance.color
                })}
              onmouseleave={() => (inspected = null)}
              onfocus={() =>
                (inspected = {
                  id: spell.id,
                  name: spell.name,
                  detail: appearance.label,
                  color: appearance.color
                })}
              onblur={() => (inspected = null)}
              aria-pressed={!hidden.has(spell.id)}
              aria-label={spell.name}
              title={`${spell.name} (${spell.id})${isPowerWindow(spell) ? ' · DPS / power window affecting this player' : ''}`}
              onclick={() => toggleSpell(spell.id)}
            >
              {#if spellIconUrl(spell.icon)}
                <img src={spellIconUrl(spell.icon)} alt="" loading="lazy" />
              {/if}
              {#if showSpellNames || !spellIconUrl(spell.icon)}<span>{spell.name}</span>{/if}
            </button>
          {/each}
        </div>
      </div>
    {/each}
  </div>
</details>

<style lang="scss">
  @use '../styles/selection-tokens' as selectionTokens;

  .spell-selection {
    @include selectionTokens.apply-selection-tokens;
  }

  @media (max-width: 600px) {
    .spell-selection .spell-actions {
      margin-bottom: 6px;
    }
    .spell-selection .spell-actions > :global(.form-field) {
      min-width: 0;
      width: 55%;
    }
    .spell-selection .spell-presets {
      width: 100%;
    }
    .spell-selection .spell-presets :global(button.spell-selection-button),
    .spell-selection .spell-actions > :global(button.spell-selection-button) {
      min-height: 44px;
    }
    .spell-selection .spell-buttons :global(button.spell-selection-button) {
      min-width: 32px;
      min-height: 32px;
      justify-content: center;
    }
  }
  .spell-group-row {
    display: flex;
    flex-wrap: wrap;
    gap: 8px 16px;
  }
  .spell-group {
    min-width: 0;
  }
  .spell-group :global(.group-toggle.spell-selection-button) {
    margin: 6px 0 3px;
  }
  .spell-buttons :global(button.spell-highlighted.spell-selection-button) {
    outline: 2px solid var(--text);
    outline-offset: 2px;
    opacity: 1;
    z-index: 4;
  }
  .spell-buttons :global(button.spell-dimmed.spell-selection-button) {
    opacity: 0.18;
  }
  .spell-buttons :global(button.power-window.spell-selection-button) {
    box-shadow: inset 0 -2px var(--spell-color);
  }
  .spell-actions,
  .spell-buttons {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 6px;
  }
  summary {
    font-size: 12px;
    font-weight: 600;
  }
  :global(button.spell-selection-button:where([data-tone='unstyled'])) {
    color: var(--text);
    background: var(--panel);
    border: 1px solid var(--line);
    border-radius: 3px;
    padding: 5px 8px;
    font: inherit;
    font-size: 12px;
  }
  :global(button.spell-selection-button:where([data-tone='unstyled'])) {
    cursor: pointer;
  }
  :global(button.spell-selection-button:where([data-tone='unstyled']):disabled) {
    opacity: 0.5;
    cursor: wait;
  }
  :global(button.spell-selection-button:where([data-tone='unstyled']):hover:not(:disabled)),
  :global(button.spell-selection-button:where([data-tone='unstyled']):focus-visible) {
    border-color: var(--accent);
  }
  .spell-selection {
    border: 1px solid var(--line);
    padding: 8px;
    border-radius: 4px;
  }
  summary {
    cursor: pointer;
    margin-bottom: 5px;
  }
  .spell-buttons {
    margin-top: 3px;
    max-height: 132px;
    overflow-y: auto;
  }
  @media (max-width: 600px), (pointer: coarse) {
    .spell-selection .spell-buttons {
      max-height: none;
      overflow-y: visible;
    }
  }
  .spell-buttons :global(button.spell-selection-button) {
    display: inline-flex;
    align-items: center;
    gap: 5px;
    padding: 2px;
    border-color: var(--spell-color);
    box-shadow: inset 3px 0 var(--spell-color);
  }
  .spell-buttons img {
    width: 23px;
    height: 23px;
    border-radius: 2px;
  }
  .spell-buttons :global(.muted.spell-selection-button) {
    opacity: 0.45;
    filter: grayscale(1);
    border-color: var(--line);
  }
  .spell-presets {
    display: flex;
    flex-wrap: wrap;
    gap: 4px;
  }
  .spell-presets :global(button.spell-selection-button) {
    display: inline-flex;
    align-items: center;
    gap: 5px;
  }
  .spell-presets img {
    width: 20px;
    height: 20px;
    border-radius: 2px;
  }
</style>
