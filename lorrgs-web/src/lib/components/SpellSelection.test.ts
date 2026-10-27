import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';
import SpellSelection from './SpellSelection.svelte';
import { rows, response } from '../testing/timelineFixtures';

describe('SpellSelection', () => {
  it('clears spells explicitly and stays cleared when clicked again', async () => {
    render(SpellSelection, {
      timelines: [response(rows[0].report)],
      classId: 2,
      edition: 'anniversary'
    });
    const clear = screen.getByRole('button', { name: 'Clear spells' });
    expect(clear).not.toHaveAttribute('aria-pressed');
    for (let click = 0; click < 2; click++) {
      await fireEvent.click(clear);
      expect(clear).not.toHaveAttribute('aria-pressed');
      expect(screen.getByRole('button', { name: 'Everything' })).toHaveAttribute(
        'aria-pressed',
        'false'
      );
      expect(screen.getByRole('button', { name: 'Avenging Wrath' })).toHaveAttribute(
        'aria-pressed',
        'false'
      );
    }
    await fireEvent.click(screen.getByRole('button', { name: 'Everything' }));
    expect(clear).not.toHaveAttribute('aria-pressed');
    expect(screen.getByRole('button', { name: 'Avenging Wrath' })).toHaveAttribute(
      'aria-pressed',
      'true'
    );
  });

  it('clears every active preset and reapplies it on the next click', async () => {
    const timeline = {
      ...response(rows[0].report),
      spells: [
        { id: 35395, name: 'Crusader Strike', icon: null },
        { id: 20925, name: 'Holy Shield', icon: null },
        { id: 635, name: 'Holy Light', icon: null },
        { id: 28507, name: 'Haste Potion', icon: null }
      ]
    };
    const { container } = render(SpellSelection, {
      timelines: [timeline],
      classId: 2,
      edition: 'anniversary'
    });
    for (const button of screen
      .getByRole('group', { name: 'Spell presets' })
      .querySelectorAll('button')) {
      if (button.getAttribute('aria-pressed') !== 'true') await fireEvent.click(button);
      const selected = Array.from(container.querySelectorAll('.spell-buttons button')).map(
        (spell) => spell.getAttribute('aria-pressed')
      );
      expect(selected).toContain('true');
      await fireEvent.click(button);
      expect(button).toHaveAttribute('aria-pressed', 'false');
      for (const spell of container.querySelectorAll('.spell-buttons button'))
        expect(spell).toHaveAttribute('aria-pressed', 'false');
      await fireEvent.click(button);
      expect(button).toHaveAttribute('aria-pressed', 'true');
      expect(
        Array.from(container.querySelectorAll('.spell-buttons button')).map((spell) =>
          spell.getAttribute('aria-pressed')
        )
      ).toEqual(selected);
    }
  });

  it('clears spells when Everything is clicked again and restores them on the next click', async () => {
    const timeline = response(rows[0].report);
    const { rerender } = render(SpellSelection, {
      timelines: [timeline],
      classId: 2,
      edition: 'anniversary'
    });
    const everything = screen.getByRole('button', { name: 'Everything' });
    expect(everything).toHaveAttribute('aria-pressed', 'true');
    await fireEvent.click(everything);
    expect(everything).toHaveAttribute('aria-pressed', 'false');
    expect(screen.getByRole('button', { name: 'Avenging Wrath' })).toHaveAttribute(
      'aria-pressed',
      'false'
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
    expect(screen.getByRole('button', { name: 'Crusader Strike' })).toHaveAttribute(
      'aria-pressed',
      'false'
    );
    await fireEvent.click(everything);
    expect(everything).toHaveAttribute('aria-pressed', 'true');
    for (const name of ['Avenging Wrath', 'Crusader Strike'])
      expect(screen.getByRole('button', { name })).toHaveAttribute('aria-pressed', 'true');
  });

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
