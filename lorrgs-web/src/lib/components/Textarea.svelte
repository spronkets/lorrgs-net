<script lang="ts">
  import type { HTMLTextareaAttributes } from 'svelte/elements';
  import FormField from './FormField.svelte';

  const generatedId = $props.id();
  let {
    value = $bindable(''), label, id = generatedId, monospace = false, class: className = '', ...attributes
  }: HTMLTextareaAttributes & { label?: string; monospace?: boolean } = $props();
</script>

<FormField {label} {id}>
  <textarea {...attributes} {id} bind:value class={`textarea ${className}`} class:monospace></textarea>
</FormField>

<style lang="scss">
  @use '../styles/selection-tokens' as selectionTokens;

  .textarea {
    @include selectionTokens.selection-control;

    width: 100%;
    resize: vertical;
  }
  .textarea::placeholder {
    color: var(--select-text);
    opacity: 0.6;
  }
  .monospace {
    font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
  }
</style>
