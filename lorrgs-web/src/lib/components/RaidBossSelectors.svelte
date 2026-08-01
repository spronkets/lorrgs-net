<script lang="ts">
  export let raidPhaseGroups: Array<{
    key: string;
    label: string;
    raids: Array<{ slug: string; name: string }>;
  }> = [];
  export let bosses: Array<{ slug: string; name: string; mapped?: boolean }> = [];

  export let selectedRaid = '';
  export let selectedBoss = '';

  export let raidLabel = 'Raid';
  export let bossLabel = 'Boss';
  export let raidPlaceholder = 'Select a raid...';
  export let bossPlaceholder = 'Select a boss...';

  $: if (selectedRaid && !selectedBoss && bosses.length > 0) {
    selectedBoss = bosses[0].slug;
  }

  function handleRaidChange() {
    // Clear first; reactive block will auto-select first boss from the updated raid list.
    selectedBoss = '';
  }
</script>

<div class="raid-boss-row">
  <label>
    <span>{raidLabel}</span>
    <select bind:value={selectedRaid} on:change={handleRaidChange}>
      <option value="">{raidPlaceholder}</option>
      {#each raidPhaseGroups as group (group.key)}
        <optgroup label={group.label}>
          {#each group.raids as raid (raid.slug)}
            <option value={raid.slug}>{raid.name}</option>
          {/each}
        </optgroup>
      {/each}
    </select>
  </label>

  <label>
    <span>{bossLabel}</span>
    <select bind:value={selectedBoss} disabled={!selectedRaid}>
      <option value="">{bossPlaceholder}</option>
      {#each bosses as boss, index (`${boss.slug}-${index}`)}
        <option value={boss.slug}>
          {boss.name}
        </option>
      {/each}
    </select>
  </label>
</div>

<style lang="scss">
  .raid-boss-row {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: var(--space-5, 0.8rem);
    width: 100%;
  }

  label {
    min-width: 0;
    display: flex;
    flex-direction: column;
    gap: var(--space-2, 0.5rem);
    font-size: var(--label-font-size, 0.9rem);
    color: var(--section-heading);
    font-weight: 600;
  }

  label span {
    font-size: var(--meta-font-size, 0.82rem);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  select {
    appearance: none;
    -webkit-appearance: none;
    width: 100%;
    min-width: 0;
    padding: var(--select-padding-y, 0.52rem) var(--select-padding-x, 0.62rem);
    padding-right: var(--select-padding-right, 2.1rem);
    background: var(--select-bg);
    background-image: var(--select-chevron-icon);
    background-repeat: no-repeat;
    background-position: right var(--select-chevron-offset, 0.72rem) center;
    background-size: var(--select-chevron-size, 0.72rem);
    border: 1px solid var(--select-border);
    color: var(--select-text);
    border-radius: var(--radius-md, 0.58rem);
    font-size: var(--control-font-size, 0.95rem);
    transition:
      border-color var(--transition-fast, 0.15s),
      box-shadow var(--transition-fast, 0.15s),
      background-color var(--transition-fast, 0.15s),
      color var(--transition-fast, 0.15s);
  }

  select:hover:not(:disabled) {
    border-color: var(--select-hover-border);
    background-color: var(--select-hover-bg);
    box-shadow: var(--select-hover-ring);
  }

  select:focus {
    outline: none;
    border-color: var(--select-focus-border);
    box-shadow: var(--select-focus-ring);
    background-color: var(--select-hover-bg);
  }

  select:disabled {
    background-color: var(--disabled-bg);
    background-image: var(--disabled-chevron-icon);
    border-color: var(--disabled-border);
    color: var(--disabled-text);
    -webkit-text-fill-color: var(--disabled-text);
    cursor: not-allowed;
    opacity: 1;
    box-shadow: var(--disabled-icon-ring);
  }

  option {
    background: #ffffff;
    color: var(--select-text);
    padding: 0.5rem;
  }

  option:checked {
    background: #d5e8f8;
    color: #15334f;
  }

  :global(.theme-dark) option {
    background: #13283d;
    color: #d5e8f8;
  }

  :global(.theme-dark) option:checked {
    background: #1f3f5f;
    color: #eef8ff;
  }

  @media (max-width: 700px) {
    .raid-boss-row {
      grid-template-columns: 1fr;
    }
  }
</style>
