<script lang="ts">
  import { LoaderCircle } from '@lucide/svelte';
  type Props = {
    loadingText?: string;
    inline?: boolean;
  };
  let { loadingText = '', inline = false }: Props = $props();
</script>

<span
  class="spinner"
  class:inline
  role={inline ? undefined : 'status'}
  aria-label={!inline && !loadingText ? 'Loading' : undefined}
>
  <LoaderCircle class="loading-spinner" strokeWidth={3} aria-hidden="true" focusable="false" />
  {#if loadingText}<span class="loading-text">{loadingText}</span>{/if}
</span>

<style>
  .spinner {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 1rem;
    width: 100%;
    padding-block: 3rem;
    text-align: center;
  }
  .loading-text {
    font-size: 0.875rem;
    line-height: 1.5;
  }
  .spinner :global(.loading-spinner) {
    display: inline-block;
    width: 3rem;
    height: 3rem;
    flex-shrink: 0;
    animation: spin 0.8s linear infinite;
  }
  .inline {
    display: inline-flex;
    width: auto;
    padding: 0;
    margin-inline-end: 0.4em;
    vertical-align: -0.125em;
  }
  .inline :global(.loading-spinner) {
    width: 1em;
    height: 1em;
  }
  @keyframes spin {
    to {
      transform: rotate(360deg);
    }
  }
  @media (prefers-reduced-motion: reduce) {
    .spinner :global(.loading-spinner) {
      animation: none;
    }
  }
</style>
