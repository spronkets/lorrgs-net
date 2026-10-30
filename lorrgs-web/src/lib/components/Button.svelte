<script lang="ts">
  import type { Snippet } from 'svelte';
  import type { HTMLButtonAttributes } from 'svelte/elements';

  type Tone = 'primary' | 'secondary' | 'success' | 'warning' | 'danger';
  type Props = Omit<HTMLButtonAttributes, 'children'> & {
    variant?: Tone | `${Tone}-outline` | 'unstyled';
    size?: 'small' | 'medium' | 'large';
    element?: HTMLButtonElement;
    children: Snippet;
  };

  let {
    variant = 'primary',
    size = 'medium',
    type = 'button',
    class: className = '',
    element = $bindable(),
    children,
    ...attributes
  }: Props = $props();
  const resolvedVariant = $derived(
    (variant === 'primary-outline' || variant === 'secondary-outline') &&
      (attributes['aria-pressed'] === true || attributes['aria-pressed'] === 'true')
      ? 'primary'
      : (variant === 'primary-outline' || variant === 'secondary-outline') &&
          (attributes['aria-pressed'] === false || attributes['aria-pressed'] === 'false')
        ? 'secondary-outline'
        : variant
  );
</script>

<button
  bind:this={element}
  {...attributes}
  {type}
  class={['button', className]}
  data-tone={resolvedVariant.replace('-outline', '')}
  data-outline={resolvedVariant.endsWith('-outline')}
  data-size={size}
>
  {@render children()}
</button>

<style lang="scss">
  @use '../styles/selection-tokens' as selectionTokens;

  .button:not([data-tone='unstyled']) {
    @include selectionTokens.apply-selection-tokens;
    @include selectionTokens.control-base;

    --button-color: #215f9b;
    --button-outline-color: var(--button-color);
    display: inline-flex;
    align-items: center;
    justify-content: center;
    gap: 0.4rem;
    width: fit-content;
    border-color: var(--button-color);
    background: var(--button-color);
    color: #fff;
    font-weight: 600;
    text-decoration: none;
    cursor: pointer;
  }
  .button[data-tone='secondary'] {
    --button-color: #526579;
  }
  .button[data-tone='success'] {
    --button-color: #237044;
  }
  .button[data-tone='warning'] {
    --button-color: #75500f;
  }
  .button[data-tone='danger'] {
    --button-color: #a52c36;
  }
  :global(.theme-dark) .button {
    --button-outline-color: #85bfff;
  }
  :global(.theme-dark) .button[data-tone='secondary'] {
    --button-outline-color: #b6c8da;
  }
  :global(.theme-dark) .button[data-tone='success'] {
    --button-outline-color: #7ddca0;
  }
  :global(.theme-dark) .button[data-tone='warning'] {
    --button-outline-color: #f3d392;
  }
  :global(.theme-dark) .button[data-tone='danger'] {
    --button-outline-color: #ff8c96;
  }
  .button[data-outline='true'] {
    background: transparent;
    color: var(--button-outline-color);
    border-color: currentColor;
  }
  .button:not([data-tone='unstyled']):hover:not(:disabled) {
    background: color-mix(in srgb, var(--button-color) 85%, #000);
  }
  .button[data-outline='true']:hover:not(:disabled) {
    background: color-mix(in srgb, var(--button-outline-color) 12%, transparent);
    box-shadow: 0 0 0 1px color-mix(in srgb, var(--button-outline-color) 26%, transparent);
  }
  .button:not([data-tone='unstyled']):active:not(:disabled) {
    box-shadow: inset 0 2px 4px #0003;
  }
  .button:not([data-tone='unstyled']):focus-visible {
    outline: none;
    border-color: var(--button-outline-color);
    box-shadow: 0 0 0 2px color-mix(in srgb, var(--button-outline-color) 22%, transparent);
  }
  .button:not([data-tone='unstyled']):disabled {
    background: var(--disabled-bg);
    border-color: var(--disabled-border);
    color: var(--disabled-text);
    opacity: 1;
    cursor: not-allowed;
  }
  .button:not([data-tone='unstyled'])[data-size='small'] {
    min-height: 44px;
    padding: 0.3rem 0.6rem;
    font-size: 0.85rem;
  }
  .button:not([data-tone='unstyled'])[data-size='large'] {
    min-height: 44px;
    padding: 0.3rem 1rem;
    font-size: 1.05rem;
  }
  .button[data-tone='secondary'][data-outline='true']:not(:disabled) {
    background: var(--select-bg);
    border-color: var(--select-border);
    color: var(--select-text);
  }
  .button[data-tone='secondary'][data-outline='true']:hover:not(:disabled) {
    background: var(--select-hover-bg);
    border-color: var(--select-hover-border);
    box-shadow: var(--select-hover-ring);
  }
  .button[data-tone='secondary'][data-outline='true']:focus-visible {
    background: var(--select-hover-bg);
    border-color: var(--select-focus-border);
    box-shadow: var(--select-focus-ring);
  }
  @media (prefers-reduced-motion: reduce) {
    .button {
      transition: none;
    }
  }
</style>
