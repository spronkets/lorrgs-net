<script lang="ts">
  import type { HTMLInputAttributes } from 'svelte/elements';

  type Props = Omit<HTMLInputAttributes, 'type' | 'checked'> & {
    label: string;
    checked?: boolean;
  };

  const generatedId = $props.id();
  let {
    label,
    checked = $bindable(false),
    id = generatedId,
    disabled = false,
    class: className = '',
    ...attributes
  }: Props = $props();
</script>

<label class="checkbox-control" for={id}>
  <input
    {...attributes}
    {id}
    {disabled}
    type="checkbox"
    bind:checked
    class={['checkbox', className]}
  />
  <span>{label}</span>
</label>

<style lang="scss">
  @use '../styles/selection-tokens' as selectionTokens;

  .checkbox-control {
    @include selectionTokens.apply-selection-tokens;
    display: inline-flex;
    align-items: center;
    gap: 0.5rem;
    cursor: pointer;
    white-space: nowrap;
    color: var(--select-text);
    font-size: var(--control-font-size);
    line-height: 1.4;
  }
  .checkbox-control:has(:disabled) {
    color: var(--disabled-text);
    cursor: not-allowed;
  }
  .checkbox {
    box-sizing: border-box;
    appearance: none;
    display: grid;
    place-content: center;
    width: 16px;
    height: 16px;
    margin: 0;
    border: 1px solid var(--select-border);
    border-radius: 3px;
    background: var(--select-bg);
    flex-shrink: 0;
    cursor: inherit;
  }
  .checkbox-control:hover .checkbox:not(:disabled) {
    border-color: var(--select-hover-border);
    box-shadow: var(--select-hover-ring);
  }
  .checkbox:checked {
    background: var(--select-focus-border);
    border-color: var(--select-focus-border);
  }
  .checkbox:checked::before {
    content: '';
    width: 4px;
    height: 8px;
    border: solid var(--select-bg);
    border-width: 0 2px 2px 0;
    transform: translateY(-1px) rotate(45deg);
  }
  .checkbox:focus-visible {
    outline: none;
    border-color: var(--select-focus-border);
    box-shadow: var(--select-focus-ring);
  }
  .checkbox:disabled {
    background: var(--disabled-bg);
    border-color: var(--disabled-border);
  }
  .checkbox:disabled::before {
    border-color: var(--disabled-text);
  }
  @media (forced-colors: active) {
    .checkbox {
      appearance: auto;
    }
    .checkbox::before {
      content: none;
    }
    .checkbox:focus-visible {
      outline: 2px solid Highlight;
    }
  }
</style>
