<script lang="ts">
  import { X } from '@lucide/svelte';
  type Props = {
    message: string;
    variant?: 'success' | 'error';
    onclose: () => void;
  };

  let { message, variant = 'success', onclose }: Props = $props();

  $effect(() => {
    if (!message) return;
    const timer = setTimeout(onclose, 4000);
    return () => clearTimeout(timer);
  });
</script>

<div
  class="toast"
  class:error={variant === 'error'}
  role={variant === 'error' ? 'alert' : 'status'}
  aria-atomic="true"
>
  <span>{message}</span>
  <button
    class={['button', ['toast-button']]}
    data-tone="unstyled"
    data-outline="false"
    data-size="medium"
    type="button"
    aria-label="Dismiss notification"
    onclick={onclose}
  >
    <X size={12} aria-hidden="true" />
  </button>
</div>

<style>
  .toast {
    position: fixed;
    right: 20px;
    bottom: 20px;
    z-index: 200;
    display: flex;
    align-items: center;
    gap: 12px;
    max-width: calc(100vw - 40px);
    padding: 10px 14px;
    border: 1px solid var(--line, #bdc9d4);
    border-radius: 6px;
    background: var(--panel, #f4f6f8);
    color: var(--text, #182c40);
    box-shadow: 0 4px 18px #0006;
    font-size: 13px;
  }
  :global(.theme-dark) .toast {
    border-color: var(--line, #4a4b4d);
    background: var(--panel, #202020);
    color: var(--text, #eeeae4);
  }
  .toast.error {
    border-color: #ce4141;
  }
  :global(button.toast-button) {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 24px;
    height: 24px;
    padding: 0;
    flex-shrink: 0;
    color: inherit;
    background: transparent;
    border: 1px solid var(--line, #bdc9d4);
    border-radius: 3px;
    cursor: pointer;
  }
  :global(button.toast-button:hover),
  :global(button.toast-button:focus-visible) {
    border-color: var(--accent, #a32662);
  }
</style>
