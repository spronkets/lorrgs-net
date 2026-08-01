<script lang="ts">
  import { createEventDispatcher } from 'svelte';

  export let versions: string[] = [];
  export let selectedVersion = '';
  export let getVersionIconUrl: (_version: string) => string;
  export let variant: 'cards' | 'select' = 'cards';
  export let placeholder = '— choose an edition —';

  const dispatch = createEventDispatcher<{ select: { version: string } }>();

  function onSelect(version: string) {
    if (!version || selectedVersion === version) return;
    dispatch('select', { version });
  }
</script>

{#if variant === 'select'}
  <div class="edition-select-wrap" aria-label="Edition selection">
    <select
      class="edition-select"
      value={selectedVersion}
      on:change={(event) => onSelect((event.currentTarget as HTMLSelectElement).value)}
    >
      <option value="">{placeholder}</option>
      {#each versions as version (version)}
        <option value={version}>{version}</option>
      {/each}
    </select>
  </div>
{:else}
  <div class="icon-strip" aria-label="Edition selection">
    {#each versions as version (version)}
      <button
        class="icon-card"
        class:version-card={true}
        class:active={selectedVersion === version}
        on:click={() => onSelect(version)}
        title={version}
        type="button"
      >
        <span class="banner-frame">
          <span class="banner-image-stack">
            <img
              src={getVersionIconUrl(version)}
              alt={version}
              class="selector-icon banner banner-base"
            />
            <img
              src={getVersionIconUrl(version)}
              alt=""
              aria-hidden="true"
              class="selector-icon banner banner-text-boost"
            />
          </span>
        </span>
      </button>
    {/each}
  </div>
{/if}

<style lang="scss">
  .edition-select-wrap {
    width: 100%;
  }

  .edition-select {
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

  .edition-select:hover {
    border-color: var(--select-hover-border);
    background-color: var(--select-hover-bg);
    box-shadow: var(--select-hover-ring);
  }

  .edition-select:focus {
    outline: none;
    border-color: var(--select-focus-border);
    box-shadow: var(--select-focus-ring);
    background-color: var(--select-hover-bg);
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

  .icon-strip {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
    gap: var(--space-4, 0.75rem);
  }

  .icon-card {
    display: flex;
    flex-direction: column;
    align-items: stretch;
    gap: var(--space-1, 0.32rem);
    background: transparent;
    border: none;
    border-radius: 0;
    padding: 0;
    cursor: pointer;
    transition: none;
  }

  .icon-card.version-card {
    align-items: stretch;
    padding: 0;
    border: none;
    background: transparent;
    box-shadow: none;
    border-radius: 0;
    transition: none;
  }

  .selector-icon {
    display: block;
    width: 100%;
    height: 100%;
    aspect-ratio: 4 / 3;
    object-fit: contain;
  }

  .banner-image-stack {
    position: relative;
    display: block;
  }

  .banner-frame {
    border: 1px solid var(--select-border);
    border-radius: var(--radius-md, 0.58rem);
    padding: var(--space-1, 0.32rem);
    background: var(--select-bg);
    transition:
      border-color var(--transition-fast, 0.15s),
      box-shadow var(--transition-fast, 0.15s),
      background-color var(--transition-fast, 0.15s);
  }

  .selector-icon.banner {
    filter: var(--edition-logo-filter, none);
    transition: none;
  }

  .selector-icon.banner-base {
    position: relative;
    z-index: 1;
  }

  .selector-icon.banner-text-boost {
    position: absolute;
    inset: 0;
    z-index: 2;
    pointer-events: none;
    opacity: var(--edition-logo-boost-opacity, 0);
    clip-path: var(--edition-logo-boost-clip, inset(56% 8% 5% 8%));
    filter: var(--edition-logo-boost-filter, none);
    mix-blend-mode: multiply;
  }

  .icon-card.version-card:not(.active):hover .banner-frame {
    border-color: var(--select-hover-border);
    box-shadow: var(--select-hover-ring);
    background: var(--select-hover-bg);
  }

  .icon-card.version-card:not(.active):hover .selector-icon.banner-base {
    filter: var(--edition-logo-hover-filter, none);
  }

  .icon-card.version-card:not(.active):hover .selector-icon.banner-text-boost {
    filter: var(--edition-logo-boost-hover-filter, var(--edition-logo-boost-filter, none));
  }

  .icon-card.version-card:hover {
    transform: none;
  }

  .icon-card.version-card.active .banner-frame {
    border-color: var(--edition-card-active-border, var(--select-focus-border));
    box-shadow: var(--edition-card-active-ring, var(--select-focus-ring));
    background: var(--edition-card-bg, var(--select-bg));
  }

  .icon-card.version-card.active .selector-icon.banner-base {
    filter: var(--edition-logo-hover-filter, none);
  }

  .icon-card.version-card.active .selector-icon.banner-text-boost {
    filter: var(--edition-logo-boost-hover-filter, var(--edition-logo-boost-filter, none));
  }

  .icon-card.version-card:focus {
    outline: none;
  }

  .icon-card.version-card:focus-visible {
    outline: none;
  }

  .icon-card.version-card:focus-visible .banner-frame {
    border-color: var(--edition-card-active-border, var(--select-focus-border));
    box-shadow: var(--edition-card-active-ring, var(--select-focus-ring));
    background: var(--edition-card-bg, var(--select-bg));
  }
</style>
