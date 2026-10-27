import { fireEvent, render, screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';
import Toast from './Toast.svelte';

afterEach(() => vi.useRealTimers());

describe('Toast', () => {
  it('announces success and dismisses through its icon button', async () => {
    const onclose = vi.fn();
    render(Toast, { message: 'Copied casts.', onclose });
    expect(screen.getByRole('status')).toHaveTextContent('Copied casts.');
    const button = screen.getByRole('button', { name: 'Dismiss notification' });
    expect(button.querySelector('svg')).not.toBeNull();
    await fireEvent.click(button);
    expect(onclose).toHaveBeenCalledOnce();
  });

  it('announces errors as alerts', () => {
    render(Toast, { message: 'Clipboard permission denied.', variant: 'error', onclose: vi.fn() });
    expect(screen.getByRole('alert')).toHaveTextContent('Clipboard permission denied.');
    expect(screen.getByRole('alert')).toHaveClass('error');
  });

  it('auto-dismisses after four seconds and resets the timer for a new message', async () => {
    vi.useFakeTimers();
    const onclose = vi.fn();
    const { rerender } = render(Toast, { message: 'First notification.', onclose });
    await vi.advanceTimersByTimeAsync(3000);
    expect(onclose).not.toHaveBeenCalled();
    await rerender({ message: 'Second notification.', onclose });
    await vi.advanceTimersByTimeAsync(3999);
    expect(onclose).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(1);
    expect(onclose).toHaveBeenCalledOnce();
  });

  it('clears its timer when unmounted', async () => {
    vi.useFakeTimers();
    const onclose = vi.fn();
    const { unmount } = render(Toast, { message: 'Notification.', onclose });
    unmount();
    await vi.advanceTimersByTimeAsync(4000);
    expect(onclose).not.toHaveBeenCalled();
  });
});
