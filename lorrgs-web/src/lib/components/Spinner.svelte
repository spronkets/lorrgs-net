<script lang="ts">
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
  <svg
    class="loading-spinner"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="3"
    aria-hidden="true"
    focusable="false"
  >
    <circle cx="12" cy="12" r="9" opacity="0.25" />
    <path d="M12 3a9 9 0 0 1 9 9" stroke-linecap="round" />
  </svg>
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
  .loading-spinner {
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
  .inline .loading-spinner {
    width: 1em;
    height: 1em;
  }
  @keyframes spin {
    to {
      transform: rotate(360deg);
    }
  }
  @media (prefers-reduced-motion: reduce) {
    .loading-spinner {
      animation: none;
    }
  }
</style>
