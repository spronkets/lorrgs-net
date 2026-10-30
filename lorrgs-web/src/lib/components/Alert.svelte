<script lang="ts">
  import { CircleX, Info, TriangleAlert } from '@lucide/svelte';
  import type { Snippet } from 'svelte';

  type Props = {
    variant?: 'error' | 'warning' | 'info';
    compact?: boolean;
    children: Snippet;
  };

  let { variant = 'info', compact = false, children }: Props = $props();
</script>

<div
  class="alert"
  class:compact
  data-variant={variant}
  role={variant === 'error' ? 'alert' : 'status'}
  aria-atomic="true"
>
  {#if variant === 'warning'}
    <TriangleAlert class="icon" strokeWidth={1.8} aria-hidden="true" />
  {:else if variant === 'error'}
    <CircleX class="icon" strokeWidth={1.8} aria-hidden="true" />
  {:else}
    <Info class="icon" strokeWidth={1.8} aria-hidden="true" />
  {/if}
  <div class="content">{@render children()}</div>
</div>

<style lang="scss">
  @use '../styles/colors';

  .alert {
    --alert-color: #{colors.$info-text};
    --alert-bg: #{colors.$info-background};
    --alert-border: #{colors.$info-border};
    display: flex;
    align-items: center;
    gap: 10px;
    box-sizing: border-box;
    min-width: 0;
    padding: 12px 14px;
    margin: 8px 0;
    border: 1px solid var(--alert-border);
    border-radius: 6px;
    background: var(--alert-bg);
    color: var(--alert-color);
    font-size: 0.875rem;
    line-height: 1.5;
  }
  .alert[data-variant='error'] {
    --alert-color: #{colors.$danger-text};
    --alert-bg: #{colors.$danger-background};
    --alert-border: #{colors.$danger-border};
  }
  .alert[data-variant='warning'] {
    --alert-color: #{colors.$warning};
    --alert-bg: #{colors.$warning-background};
    --alert-border: #{colors.$warning-border};
  }
  :global(.theme-dark) .alert {
    --alert-color: #{colors.$info-text-dark};
    --alert-bg: #{colors.$info-background-dark};
    --alert-border: #{colors.$info-border-dark};
  }
  :global(.theme-dark) .alert[data-variant='error'] {
    --alert-color: #{colors.$danger-text-dark};
    --alert-bg: #{colors.$danger-background-dark};
    --alert-border: #{colors.$danger-border-dark};
  }
  :global(.theme-dark) .alert[data-variant='warning'] {
    --alert-color: #{colors.$warning-dark};
    --alert-bg: #{colors.$warning-background-dark};
    --alert-border: #{colors.$warning-border-dark};
  }
  .compact {
    padding: 4px 8px;
    margin: 0;
    gap: 6px;
    font-size: 12px;
  }
  .alert :global(.icon) {
    width: 18px;
    height: 18px;
    flex-shrink: 0;
  }
  .content {
    min-width: 0;
    overflow-wrap: anywhere;
  }
</style>
