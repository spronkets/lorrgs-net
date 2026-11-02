import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';
import SpecSelector from './SpecSelector.svelte';

const options = [
  {
    slug: 'guardian-druid',
    fullName: 'Guardian Druid',
    roleName: 'Tank',
    classSlug: 'druid',
    specSlug: 'guardian'
  },
  {
    slug: 'restoration-druid',
    fullName: 'Restoration Druid',
    roleName: 'Healer',
    classSlug: 'druid',
    specSlug: 'restoration'
  },
  {
    slug: 'affliction-warlock',
    fullName: 'Affliction Warlock',
    roleName: 'DPS',
    classSlug: 'warlock',
    specSlug: 'affliction'
  },
  {
    slug: 'arcane-mage',
    fullName: 'Arcane Mage',
    roleName: 'DPS',
    classSlug: 'mage',
    specSlug: 'arcane'
  }
];

describe('SpecSelector', () => {
  it('shows a placeholder when there are no specializations', () => {
    render(SpecSelector, { options: [], placeholder: '— choose a specialization —' });
    expect(screen.getByText('Specialization')).toBeInTheDocument();
    expect(screen.getByText('— choose a specialization —')).toBeInTheDocument();
  });

  it('groups roles in Damage, Healers, Tanks order and sorts specs by name within each role', () => {
    render(SpecSelector, { options });
    const groups = screen.getAllByRole('heading', { level: 4 }).map((heading) => heading.textContent);
    expect(groups).toEqual(['Damage', 'Healers', 'Tanks']);

    const dpsButtons = screen.getAllByRole('button').filter((button) =>
      ['Affliction Warlock', 'Arcane Mage'].includes(button.getAttribute('aria-label') ?? '')
    );
    expect(dpsButtons.map((button) => button.getAttribute('aria-label'))).toEqual([
      'Affliction Warlock',
      'Arcane Mage'
    ]);
  });

  it('updates pressed and selected states when choosing a specialization', async () => {
    render(SpecSelector, { options, value: 'guardian-druid' });
    const guardian = screen.getByRole('button', { name: 'Guardian Druid' });
    const arcane = screen.getByRole('button', { name: 'Arcane Mage' });

    expect(guardian).toHaveAttribute('aria-pressed', 'true');
    expect(guardian).toHaveClass('selected');
    expect(arcane).toHaveAttribute('aria-pressed', 'false');

    await fireEvent.click(arcane);

    expect(arcane).toHaveAttribute('aria-pressed', 'true');
    expect(arcane).toHaveClass('selected');
    expect(guardian).toHaveAttribute('aria-pressed', 'false');
  });

  it('disables specialization buttons when disabled', async () => {
    render(SpecSelector, { options, value: 'guardian-druid', disabled: true });
    const arcane = screen.getByRole('button', { name: 'Arcane Mage' });
    const guardian = screen.getByRole('button', { name: 'Guardian Druid' });

    expect(arcane).toBeDisabled();
    expect(guardian).toHaveAttribute('aria-pressed', 'true');
    expect(arcane).toHaveAttribute('aria-pressed', 'false');
  });
});
