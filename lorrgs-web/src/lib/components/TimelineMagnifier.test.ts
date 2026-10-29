import { render, screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';
import TimelineMagnifier from './TimelineMagnifier.svelte';
import { rows, response } from '../testing/timelineFixtures';

describe('TimelineMagnifier', () => {
  it('clips intersecting buffs and distinguishes markers, deaths, and missing spell metadata', async () => {
    const timeline = response(rows[0].report);
    timeline.events = [
      { spellId: 31884, timestamp: 0, duration: 15000, kind: 'buff' },
      { spellId: 31884, timestamp: 15000, duration: null, kind: 'marker' },
      { spellId: 999, timestamp: 15000, duration: null, kind: 'cast' },
      { spellId: 0, timestamp: 18000, duration: null, kind: 'death' },
      { spellId: 31884, timestamp: 30000, duration: null, kind: 'cast' }
    ];
    const props = {
      magnifier: { timeline, start: 10000, span: 10000, x: 10, y: 10, zoom: 300 },
      inspected: null,
      hidden: new Set<number>(),
      castSpellIds: new Set<number>(),
      showCasts: true,
      showBuffs: true,
      showDeaths: true
    };
    const { container, rerender } = render(TimelineMagnifier, props);
    expect(container.querySelectorAll('.magnified-event')).toHaveLength(4);
    expect(container.querySelector('.buff')).toHaveStyle({ left: '0%', width: '50%' });
    expect(screen.getByLabelText('Inferred marker')).toBeInTheDocument();
    expect(container.querySelector('.death')).toBeInTheDocument();
    expect(container.querySelectorAll('.spell-dimmed')).toHaveLength(4);
    await rerender({ ...props, showBuffs: false, showDeaths: false });
    expect(container.querySelectorAll('.magnified-event')).toHaveLength(2);
    expect(container.querySelector('.buff, .death')).toBeNull();
  });
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
