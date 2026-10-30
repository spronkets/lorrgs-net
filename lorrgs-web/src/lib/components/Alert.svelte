<script lang="ts">
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
  <svg
    class="icon"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="1.8"
    aria-hidden="true"
  >
    {#if variant === 'warning'}
      <path d="M12 3 2 21h20L12 3Z" /><path d="M12 9v5m0 3v1" />
    {:else}
      <circle cx="12" cy="12" r="9" />
      {#if variant === 'error'}<path d="m9 9 6 6m0-6-6 6" />
      {:else}<path d="M12 11v6m0-10v1" />{/if}
    {/if}
  </svg>
  <div class="content">{@render children()}</div>
</div>

<style>
  .alert {
    --alert-color: #214f79;
    --alert-bg: #edf5fc;
    --alert-border: #8db4d5;
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
    --alert-color: #862e34;
    --alert-bg: #fff1f2;
    --alert-border: #d99ca1;
  }
  .alert[data-variant='warning'] {
    --alert-color: #75500f;
    --alert-bg: #fff8e6;
    --alert-border: #d3b36b;
  }
  :global(.theme-dark) .alert {
    --alert-color: #bddcf5;
    --alert-bg: #1b2b3b;
    --alert-border: #486b8b;
  }
  :global(.theme-dark) .alert[data-variant='error'] {
    --alert-color: #ffc2c6;
    --alert-bg: #392326;
    --alert-border: #92545b;
  }
  :global(.theme-dark) .alert[data-variant='warning'] {
    --alert-color: #f3d392;
    --alert-bg: #342d1e;
    --alert-border: #887341;
  }
  .compact {
    padding: 4px 8px;
    margin: 0;
    gap: 6px;
    font-size: 12px;
  }
  .icon {
    width: 18px;
    height: 18px;
    flex-shrink: 0;
  }
  .content {
    min-width: 0;
    overflow-wrap: anywhere;
  }
</style>
