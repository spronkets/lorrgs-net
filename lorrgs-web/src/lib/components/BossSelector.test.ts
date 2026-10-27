import { fireEvent, render, screen, within } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';
import BossSelector from './BossSelector.svelte';

const bosses = [
  { slug: 'high-warlord-najentus', name: "High Warlord Naj'entus", iconName: 'najentus' },
  { slug: 'supremus', name: 'Supremus', iconName: 'supremus' }
];

describe('BossSelector', () => {
  it('omits missing portraits in both the selected value and the open list', async () => {
    render(BossSelector, {
      bosses: [{ slug: 'unknown', name: 'Unknown boss' }, bosses[1]],
      value: 'unknown'
    });
    const trigger = screen.getByRole('button', { name: 'Boss' });
    expect(trigger.querySelector('img, svg:not(.chevron)')).toBeNull();
    await fireEvent.click(trigger);
    const options = screen.getAllByRole('option');
    expect(options[0].querySelector('img, svg')).toBeNull();
    expect(options[1].querySelector('img')).toHaveAttribute(
      'src',
      'https://wow.zamimg.com/images/wow/icons/medium/supremus.jpg'
    );
  });

  it('shows a portrait for every option and updates the selected portrait', async () => {
    render(BossSelector, { bosses, value: 'high-warlord-najentus' });
    const trigger = screen.getByRole('button', { name: 'Boss' });
    await fireEvent.click(trigger);
    const options = screen.getAllByRole('option');
    expect(options).toHaveLength(2);
    expect(options[0].querySelector('img')).toHaveAttribute(
      'src',
      'https://wow.zamimg.com/images/wow/icons/medium/najentus.jpg'
    );
    expect(options[1].querySelector('img')).toHaveAttribute(
      'src',
      'https://wow.zamimg.com/images/wow/icons/medium/supremus.jpg'
    );
    await fireEvent.click(options[1]);
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
    expect(trigger).toHaveTextContent('Supremus');
    expect(trigger.querySelector('img')).toHaveAttribute(
      'src',
      'https://wow.zamimg.com/images/wow/icons/medium/supremus.jpg'
    );
    expect(trigger).toHaveFocus();
  });

  it('supports keyboard selection, cancellation, and outside dismissal', async () => {
    render(BossSelector, { bosses, value: 'high-warlord-najentus' });
    const trigger = screen.getByRole('button', { name: 'Boss' });
    await fireEvent.keyDown(trigger, { key: 'ArrowDown' });
    let list = screen.getByRole('listbox');
    expect(list).toHaveFocus();
    await fireEvent.keyDown(list, { key: 'End' });
    expect(list).toHaveAttribute('aria-activedescendant', 'boss-option-1');
    await fireEvent.keyDown(list, { key: 'Enter' });
    expect(trigger).toHaveTextContent('Supremus');
    await fireEvent.click(trigger);
    list = screen.getByRole('listbox');
    expect(within(list).getByRole('option', { name: 'Supremus' })).toHaveAttribute(
      'aria-selected',
      'true'
    );
    await fireEvent.keyDown(list, { key: 'Home' });
    await fireEvent.keyDown(list, { key: 'Escape' });
    expect(trigger).toHaveTextContent('Supremus');
    expect(trigger).toHaveFocus();
    await fireEvent.click(trigger);
    await fireEvent.click(document.body);
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });
});
