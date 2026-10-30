<script lang="ts">
  import type { Snippet } from 'svelte';

  let { label, id, children }: { label?: string; id: string; children: Snippet } = $props();
</script>

<div class="form-field">
  {#if label}<label for={id}>{label}</label>{/if}
  {@render children()}
</div>

<style lang="scss">
  @use '../styles/selection-tokens' as selectionTokens;

  .form-field {
    @include selectionTokens.apply-selection-tokens;

    display: flex;
    flex-direction: column;
    gap: 0.5rem;
    min-width: 0;
  }
  label {
    @include selectionTokens.selection-label;
  }
</style>
