import { render, screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';
import TimelineMagnifier from './TimelineMagnifier.svelte';
import { rows, response } from '../testing/timelineFixtures';

describe('TimelineMagnifier', () => {
  it('renders a clipped zoom preview with its time bounds and event visibility options', async () => {
    const timeline = response(rows[0].report);
    const props = {
      magnifier: { timeline, start: 10000, span: 10000, x: 200, y: 160, zoom: 200 },
      inspected: { id: 31884, name: 'Avenging Wrath', color: '#fff', detail: 'test' },
      hidden: new Set<number>(),
      castSpellIds: new Set([31884]),
      showCasts: true,
      showBuffs: true,
      showDeaths: true
    };
    const { container, rerender } = render(TimelineMagnifier, props);
    expect(screen.getByRole('tooltip').getAttribute('style')).toContain('20px');
    expect(screen.getByText('Player0 · Avenging Wrath · 200%')).toBeTruthy();
    expect(screen.getByText('0:10')).toBeTruthy();
    expect(screen.getByText('0:20')).toBeTruthy();
    expect(container.querySelectorAll('.magnified-event')).toHaveLength(2);
    expect(container.querySelector('.magnified-event.spell-dimmed')).toBeNull();
    expect(container.querySelector('.magnified-event.buff')?.getAttribute('style')).toContain(
      'width: 100%'
    );
    await rerender({ ...props, showCasts: false });
    expect(container.querySelectorAll('.magnified-event')).toHaveLength(1);
    await rerender({ ...props, hidden: new Set([31884]) });
    expect(container.querySelectorAll('.magnified-event')).toHaveLength(0);
    await rerender({ ...props, magnifier: null });
    expect(screen.queryByRole('tooltip')).toBeNull();
  });
});
