import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';
import RaidBossSelectors from './RaidBossSelectors.svelte';

const props = {
  raidPhaseGroups: [
    {
      key: 'phase3',
      label: 'Phase 3',
      raids: [{ slug: 'blacktemple', name: 'Black Temple' }]
    }
  ],
  bosses: [{ slug: 'illidan', name: 'Illidan Stormrage' }]
};

describe('RaidBossSelectors', () => {
  it('disables unreleased raids while leaving available raids selectable', () => {
    render(RaidBossSelectors, {
      raidPhaseGroups: [
        {
          key: 'anniversary',
          label: 'Anniversary',
          raids: [
            { slug: 'blacktemple', name: 'Black Temple', isAvailable: true },
            { slug: 'sunwellplateau', name: 'Sunwell Plateau', isAvailable: false }
          ]
        }
      ]
    });
    expect(screen.getByRole('option', { name: 'Sunwell Plateau' })).toBeDisabled();
    expect(screen.getByRole('option', { name: 'Black Temple' })).toBeEnabled();
  });

  it('shows metadata icons without changing accessible selection controls', async () => {
    const { container, rerender } = render(RaidBossSelectors, {
      ...props,
      selectedRaid: 'blacktemple',
      selectedBoss: 'illidan',
      raidIconUrl: '/raid.jpg',
      bosses: [{ ...props.bosses[0], iconName: 'boss' }]
    });
    expect(screen.getByRole('combobox', { name: 'Raid' })).toHaveValue('blacktemple');
    expect(screen.getByRole('button', { name: 'Boss' })).toHaveTextContent('Illidan Stormrage');
    expect(container.querySelectorAll('img')).toHaveLength(2);
    expect(container.querySelector('.selection-icon img')).toHaveAttribute('src', '/raid.jpg');
    await rerender({
      ...props,
      selectedRaid: 'blacktemple',
      selectedBoss: 'illidan',
      raidIconUrl: '/other-raid.jpg',
      bosses: [{ ...props.bosses[0], iconName: 'other-boss' }]
    });
    expect(container.querySelector('.boss-selector img')).toHaveAttribute(
      'src',
      'https://wow.zamimg.com/images/wow/icons/medium/other-boss.jpg'
    );
  });

  it('omits missing raid icons, uses the generic boss icon, and preserves selection states', async () => {
    const { container } = render(RaidBossSelectors, props);
    expect(container.querySelector('.boss-selector img')).toHaveAttribute(
      'src',
      'https://wow.zamimg.com/images/icons/boss.gif'
    );
    expect(container.querySelector('.selection-icon')).toBeNull();
    expect(screen.getByRole('combobox', { name: 'Raid' })).not.toHaveClass('has-icon');
    expect(screen.getByRole('button', { name: 'Boss' })).toBeDisabled();
    await fireEvent.change(screen.getByRole('combobox', { name: 'Raid' }), {
      target: { value: 'blacktemple' }
    });
    expect(screen.getByRole('button', { name: 'Boss' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Boss' })).toHaveTextContent('Illidan Stormrage');
    expect(container.querySelector('.boss-selector img')).toHaveAttribute(
      'src',
      'https://wow.zamimg.com/images/icons/boss.gif'
    );
  });
});
