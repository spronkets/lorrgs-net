<script lang="ts">
  import { getClassIconUrl, getSpecIconUrl } from '../wowAssets';

  export let label = 'Specialization';
  export let placeholder = 'Select a specialization...';
  export let disabled = false;
  export let value = '';
  export let options: {
    slug: string;
    fullName: string;
    roleName: string;
    classSlug: string;
    specSlug: string;
  }[] = [];

  const roleOrder = ['DPS', 'Healer', 'Tank', 'Other'];
  const roleLabel: Record<string, string> = {
    DPS: 'Damage',
    Healer: 'Healers',
    Tank: 'Tanks',
    Other: 'Other'
  };
  $: groupedOptions = roleOrder
    .map((roleName) => ({
      roleName,
      label: roleLabel[roleName],
      options: options
        .filter((option) => option.roleName === roleName)
        .sort((a, b) => a.fullName.localeCompare(b.fullName))
    }))
    .filter((group) => group.options.length > 0);
</script>

<div class="spec-selector">
  <span class="field-label">{label}</span>
  {#if !groupedOptions.length}
    <div class="empty-state" aria-live="polite">{placeholder}</div>
  {:else}
    {#each groupedOptions as group (group.roleName)}
      <section class="role-group" aria-label={`${group.label} specializations`}>
        <h4 class="group-label">{group.label}</h4>
        <div class="icon-grid">
          {#each group.options as option (option.slug)}
            <button
              class:selected={value === option.slug}
              class="spec-icon-button"
              type="button"
              title={option.fullName}
              aria-label={option.fullName}
              aria-pressed={value === option.slug}
              {disabled}
              onclick={() => (value = option.slug)}
            >
              <span class="spec-media">
                <img
                  class="spec-icon"
                  src={getSpecIconUrl(option.classSlug, option.specSlug)}
                  alt=""
                />
                <img class="class-icon" src={getClassIconUrl(option.classSlug)} alt="" />
              </span>
            </button>
          {/each}
        </div>
      </section>
    {/each}
  {/if}
</div>

<style lang="scss">
  @use '../styles/selection-tokens' as selectionTokens;

  .spec-selector {
    @include selectionTokens.apply-selection-tokens;

    min-width: 0;
    display: grid;
    gap: var(--space-3, 0.7rem);
    color: var(--section-heading);
  }

  .field-label {
    @include selectionTokens.selection-label;
  }

  .empty-state {
    @include selectionTokens.selection-control;

    color: var(--summary-label);
    opacity: 0.85;
  }

  .role-group {
    display: grid;
    gap: var(--space-2, 0.5rem);
  }

  .group-label {
    margin: 0;
    font-size: 0.82rem;
    font-weight: 700;
    letter-spacing: 0.06em;
    text-transform: uppercase;
    color: var(--section-heading);
  }

  .icon-grid {
    display: flex;
    flex-wrap: wrap;
    gap: 0.45rem;
  }

  .spec-icon-button {
    @include selectionTokens.selection-control;

    width: 38px;
    height: 38px;
    min-width: 38px;
    min-height: 38px;
    padding: 0;
    border-width: 1px;
    border-radius: var(--radius-sm);
    display: flex;
    align-items: center;
    justify-content: center;
    cursor: pointer;
  }

  .spec-icon-button:disabled {
    cursor: not-allowed;
  }

  .spec-icon-button.selected {
    border-color: var(--card-border-accent);
    box-shadow: 0 0 0 1px color-mix(in srgb, var(--card-border-accent) 45%, transparent);
  }

  .spec-icon-button:focus-visible {
    outline: 2px solid var(--select-focus-border);
    outline-offset: 2px;
  }

  .spec-media {
    position: relative;
    width: 26px;
    height: 26px;
    flex-shrink: 0;
  }

  .spec-icon {
    width: 100%;
    height: 100%;
    border-radius: var(--radius-sm);
    object-fit: cover;
    border: 1px solid var(--spec-icon-border);
  }

  .class-icon {
    position: absolute;
    width: 13px;
    height: 13px;
    border-radius: 999px;
    object-fit: cover;
    right: -3px;
    bottom: -3px;
    border: 2px solid var(--class-icon-border);
    background: var(--class-icon-bg);
  }
</style>
