<script lang="ts">
  import type { HTMLInputAttributes } from 'svelte/elements';
  import type { Action } from 'svelte/action';
  import type { Snippet } from 'svelte';
  import FormField from './FormField.svelte';

  type Props = Omit<HTMLInputAttributes, 'type' | 'value'> & {
    type?: 'text' | 'number' | 'date' | 'email' | 'password' | 'search' | 'url' | 'tel';
    value?: string | number;
    label?: string;
    action?: Action<HTMLInputElement>;
    compact?: boolean;
    prefix?: Snippet;
    suffix?: Snippet;
  };
  const generatedId = $props.id();
  let {
    value = $bindable(''), type = 'text', label, id = generatedId,
    action, compact = false, prefix, suffix, class: className = '', ...attributes
  }: Props = $props();

  const enhance: Action<HTMLInputElement> = (node) => action?.(node);
</script>

<FormField {label} {id}>
  <div class="input-frame" class:affixed={prefix || suffix} class:compact>
    {#if prefix}<span class="prefix">{@render prefix()}</span>{/if}
    <input {...attributes} {id} {type} bind:value use:enhance class={`input ${className}`} class:compact />
    {#if suffix}<span class="suffix">{@render suffix()}</span>{/if}
  </div>
</FormField>

<style lang="scss">
  @use '../styles/selection-tokens' as selectionTokens;

  .input {
    @include selectionTokens.selection-control;

    width: 100%;
  }
  .input::placeholder {
    color: var(--select-text);
    opacity: 0.6;
  }
  .input-frame {
    display: flex;
    align-items: stretch;
    min-width: 0;
  }
  .affixed {
    border: 1px solid var(--select-border);
    border-radius: var(--radius-md, 0.58rem);
    background: var(--select-bg);
    color: var(--select-text);
  }
  .affixed:hover {
    border-color: var(--select-hover-border);
    background: var(--select-hover-bg);
  }
  .affixed:focus-within {
    border-color: var(--select-focus-border);
    box-shadow: var(--select-focus-ring);
  }
  .affixed .input {
    flex: 1;
    width: 0;
    min-width: 5ch;
    border: 0;
    border-radius: 0;
    background: transparent;
    box-shadow: none;
  }
  .affixed .input:focus-visible {
    outline: none;
    box-shadow: none;
  }
  .affixed input[type='number'] {
    appearance: textfield;
  }
  .affixed input::-webkit-inner-spin-button,
  .affixed input::-webkit-outer-spin-button {
    appearance: none;
    margin: 0;
  }
  .prefix,
  .suffix {
    display: inline-flex;
    align-items: center;
    flex-shrink: 0;
  }
  .prefix { padding: 3px; }
  .suffix {
    border-left: 1px solid var(--select-border);
  }
  .input.compact {
    min-height: 44px;
    padding: 0.2rem;
    font-size: 0.8rem;
  }
</style>
