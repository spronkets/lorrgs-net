<script lang="ts">
  import { ChevronDown } from '@lucide/svelte';
  import { tick } from 'svelte';
  import BossIcon from './BossIcon.svelte';

  export let bosses: { slug: string; name: string; iconName?: string }[] = [];
  export let value = '';
  export let label = 'Boss';
  export let placeholder = 'Select a boss...';
  export let disabled = false;
  let open = false;
  let active = 0;
  let root: HTMLDivElement;
  let trigger: HTMLButtonElement;
  let list: HTMLDivElement;
  $: selected = bosses.find((boss) => boss.slug === value);
  $: if (disabled) open = false;

  async function expand(
    index = Math.max(
      0,
      bosses.findIndex((boss) => boss.slug === value)
    )
  ) {
    if (disabled || !bosses.length) return;
    active = index;
    open = true;
    await tick();
    list.focus();
  }

  function choose(index: number) {
    value = bosses[index].slug;
    open = false;
    trigger.focus();
  }

  function handleKey(event: KeyboardEvent) {
    if (event.key === 'Escape') {
      event.preventDefault();
      open = false;
      trigger.focus();
    } else if (event.key === 'Tab') {
      open = false;
    } else if (['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) {
      event.preventDefault();
      active =
        event.key === 'Home'
          ? 0
          : event.key === 'End'
            ? bosses.length - 1
            : (active + (event.key === 'ArrowDown' ? 1 : -1) + bosses.length) % bosses.length;
      list.children[active]?.scrollIntoView?.({ block: 'nearest' });
    } else if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      choose(active);
    } else if (event.key.length === 1 && !event.ctrlKey && !event.metaKey && !event.altKey) {
      const index = bosses.findIndex(
        (boss, index) =>
          index > active && boss.name.toLowerCase().startsWith(event.key.toLowerCase())
      );
      const match =
        index >= 0
          ? index
          : bosses.findIndex((boss) => boss.name.toLowerCase().startsWith(event.key.toLowerCase()));
      if (match >= 0) {
        event.preventDefault();
        active = match;
        list.children[active]?.scrollIntoView?.({ block: 'nearest' });
      }
    }
  }
</script>

<svelte:window
  onclick={(event) => {
    if (open && event.target instanceof Node && !root.contains(event.target)) open = false;
  }}
/>

<div class="boss-selector" bind:this={root}>
  <span class="field-label">{label}</span>
  <button
    class={['button', ['boss-selector-button', `trigger`]]}
    data-tone="unstyled"
    data-outline="false"
    data-size="medium"
    type="button"
    bind:this={trigger}
    {disabled}
    aria-label={label}
    aria-haspopup="listbox"
    aria-expanded={open}
    onclick={() => (open ? (open = false) : expand())}
    onkeydown={(event) => {
      if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
        event.preventDefault();
        void expand();
      }
    }}
  >
    <BossIcon slug={selected?.slug ?? ''} iconName={selected?.iconName} />
    <span>{selected?.name || placeholder}</span>
    <ChevronDown class="chevron" aria-hidden="true" size={16} />
  </button>
  {#if open}
    <div
      class="options"
      role="listbox"
      tabindex="-1"
      aria-label={label}
      aria-activedescendant={`boss-option-${active}`}
      bind:this={list}
      onkeydown={handleKey}
    >
      {#each bosses as boss, index (boss.slug)}
        <div
          role="option"
          tabindex="-1"
          id={`boss-option-${index}`}
          aria-selected={value === boss.slug}
          class:active={active === index}
          onclick={() => choose(index)}
          onkeydown={handleKey}
        >
          <BossIcon slug={boss.slug} iconName={boss.iconName} />
          <span>{boss.name}</span>
        </div>
      {/each}
    </div>
  {/if}
</div>

<style lang="scss">
  @use '../styles/selection-tokens' as selectionTokens;

  .boss-selector {
    @include selectionTokens.apply-selection-tokens;

    position: relative;
    min-width: 0;
    display: flex;
    flex-direction: column;
    gap: var(--space-2, 0.5rem);
    color: var(--section-heading);
  }
  .field-label {
    font-size: 1rem;
    letter-spacing: 0.08em;
    text-transform: uppercase;
    font-weight: 600;
  }
  :global(.trigger.boss-selector-button) {
    @include selectionTokens.selection-control;

    display: flex;
    align-items: center;
    gap: 10px;
    width: 100%;
    text-align: left;
    padding-block: 6px;
    cursor: pointer;
  }
  .options:focus-visible {
    outline: 2px solid var(--select-focus-border);
    outline-offset: 2px;
  }
  .boss-selector-button :global(.chevron) {
    margin-left: auto;
    width: 16px;
    height: 16px;
  }
  .options {
    position: absolute;
    top: 100%;
    left: 0;
    right: 0;
    z-index: 20;
    max-height: 360px;
    overflow-y: auto;
    background: var(--select-bg);
    color: var(--select-text);
    border: 1px solid var(--select-border);
    border-radius: var(--radius-md, 0.58rem);
    box-shadow: 0 8px 20px #0004;
    padding: 4px;
  }
  [role='option'] {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 5px 8px;
    border-radius: 3px;
    cursor: pointer;
  }
  [role='option']:hover,
  .active {
    background: var(--select-hover-bg);
    outline: 1px solid var(--select-hover-border);
  }
  [aria-selected='true'] {
    font-weight: 700;
  }
</style>
