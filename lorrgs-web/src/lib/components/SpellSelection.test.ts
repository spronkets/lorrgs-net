import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';
import SpellSelection from './SpellSelection.svelte';
import { rows, response } from '../testing/timelineFixtures';

describe('SpellSelection', () => {
  it('applies class presets and reapplies the active preset to newly loaded spells', async () => {
    const timeline = response(rows[0].report);
    const { rerender } = render(SpellSelection, {
      timelines: [timeline],
      classId: 2,
      edition: 'anniversary'
    });
    expect(
      screen.getByRole('button', { name: 'Tank' }).querySelector('img')?.getAttribute('src')
    ).toContain('paladin-protection');
    await fireEvent.click(screen.getByRole('button', { name: 'Tank' }));
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Avenging Wrath' })).toHaveAttribute(
        'aria-pressed',
        'false'
      )
    );
    await rerender({
      timelines: [
        {
          ...timeline,
          spells: [...timeline.spells, { id: 35395, name: 'Crusader Strike', icon: null }]
        }
      ],
      classId: 2,
      edition: 'anniversary'
    });
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Crusader Strike' })).toHaveAttribute(
        'aria-pressed',
        'false'
      )
    );
    await fireEvent.click(screen.getByRole('button', { name: 'Everything' }));
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Crusader Strike' })).toHaveAttribute(
        'aria-pressed',
        'true'
      )
    );
  });

  it('filters spells, toggles groups and switches individual choices to custom', async () => {
    const { container } = render(SpellSelection, {
      timelines: [response(rows[0].report)],
      classId: 2,
      edition: 'anniversary'
    });
    const wrath = screen.getByRole('button', { name: 'Avenging Wrath' });
    await fireEvent.click(wrath);
    expect(wrath.getAttribute('aria-pressed')).toBe('false');
    expect(screen.getByRole('button', { name: 'Everything' }).getAttribute('aria-pressed')).toBe(
      'false'
    );
    await fireEvent.click(screen.getByRole('button', { name: 'Toggle DPS / power windows' }));
    expect(wrath.getAttribute('aria-pressed')).toBe('true');
    await fireEvent.input(screen.getByRole('textbox', { name: 'Search timeline spells' }), {
      target: { value: 'absent' }
    });
    expect(screen.queryByRole('button', { name: 'Avenging Wrath' })).toBeNull();
    await fireEvent.input(screen.getByRole('textbox', { name: 'Search timeline spells' }), {
      target: { value: '' }
    });
    await fireEvent.click(screen.getByRole('checkbox', { name: 'Spell names' }));
    expect(container.querySelector('.spell-buttons button span')?.textContent).toBe(
      'Avenging Wrath'
    );
  });

  it('exposes inspection on hover and focus and hides unsupported roles', async () => {
    render(SpellSelection, {
      timelines: [response(rows[0].report)],
      classId: 8,
      edition: 'anniversary'
    });
    expect(screen.queryByRole('button', { name: 'Tank' })).toBeNull();
    for (const name of ['Arcane', 'Fire', 'Frost'])
      expect(screen.getByRole('button', { name })).toBeTruthy();
    const spell = screen.getByRole('button', { name: 'Avenging Wrath' });
    await fireEvent.mouseEnter(spell);
    expect(spell).toHaveClass('spell-highlighted');
    await fireEvent.mouseLeave(spell);
    expect(spell).not.toHaveClass('spell-highlighted');
    await fireEvent.focus(spell);
    expect(spell).toHaveClass('spell-highlighted');
    await fireEvent.blur(spell);
    expect(spell).not.toHaveClass('spell-highlighted');
  });
});
