import { fireEvent, render, screen, within } from '@testing-library/svelte';
import { afterEach, expect, it, vi } from 'vitest';
import { getSpecRankings } from '../api';
import HomePage from './HomePage.svelte';

vi.mock('../api', () => ({ getSpecRankings: vi.fn() }));

afterEach(() => {
  window.history.replaceState({}, '', '/');
  vi.resetAllMocks();
});

it('combines the spec icons and encounter heading inside the selection panel', async () => {
  window.history.replaceState(
    {},
    '',
    '/?edition=Anniversary&raid=blacktemple&boss=illidan-stormrage&spec=retribution-paladin'
  );
  vi.mocked(getSpecRankings).mockResolvedValue({
    specSlug: 'retribution-paladin',
    bossSlug: 'illidan-stormrage',
    bossIconUrl: 'https://wow.zamimg.com/images/wow/icons/medium/inv_misc_questionmark.jpg',
    difficulty: 'normal',
    metric: 'dps',
    updated: '2026-10-26T12:00:00Z',
    isDirty: false,
    percentile: null,
    reports: []
  });
  const { container } = render(HomePage, {
    worldData: {
      bosses: [
        {
          id: 50609,
          nameSlug: 'illidan-stormrage',
          raidId: 1060,
          icon: 'achievement_boss_illidan.jpg'
        }
      ],
      classes: [{ id: 2, name: 'Paladin', nameSlug: 'paladin', color: '#f58cba', maskId: 2 }],
      specs: [
        {
          id: 70,
          name: 'Retribution',
          nameSlug: 'retribution',
          fullName: 'Retribution Paladin',
          fullNameSlug: 'retribution-paladin',
          classId: 2,
          roleId: 1
        }
      ],
      roles: [{ id: 1, name: 'Damage', nameSlug: 'damage', code: 'dps', color: '#ffffff' }]
    },
    raidCatalog: {
      editions: [],
      instances: {
        anniversary: [
          {
            zoneId: 1060,
            slug: 'blacktemple',
            name: 'Black Temple',
            edition: 'anniversary',
            phase: 3,
            public: true,
            bosses: [
              { id: 50609, name: 'Illidan Stormrage', slug: 'illidan-stormrage', mapped: true }
            ]
          }
        ]
      }
    }
  });
  const heading = await screen.findByRole('heading', {
    name: 'Paladin Retribution Paladin vs. Illidan Stormrage'
  });
  expect(heading.closest('.selection-collapsed')).not.toBeNull();
  expect(container.querySelector('.selection-overview .value')).toHaveTextContent(
    /^Anniversary \/ Black Temple$/
  );
  expect(heading.querySelector('.spec-icon')?.nextElementSibling).toHaveClass('class-icon');
  expect(within(heading).getByRole('img', { name: 'Paladin' })).toBeInTheDocument();
  await screen.findByRole('region', { name: 'Cooldown comparison timelines' });
  expect(vi.mocked(getSpecRankings).mock.calls[0][5]).toMatchObject({
    includeOtherPlayers: false
  });
  const expectedIcon =
    'https://wow.zamimg.com/images/wow/icons/medium/achievement_boss_illidan.jpg';
  expect(heading.querySelector('.boss-icon')).toHaveAttribute('src', expectedIcon);
  expect(container.querySelector('.rankings-header')).toBeNull();
  expect(screen.queryByText(/Updated:/)).not.toBeInTheDocument();
  await fireEvent.click(screen.getByRole('button', { name: 'Change selection' }));
  const bossSelector = screen.getByRole('button', { name: 'Boss' });
  expect(bossSelector.querySelector('img')).toHaveAttribute('src', expectedIcon);
  await fireEvent.click(bossSelector);
  expect(
    screen.getByRole('option', { name: 'Illidan Stormrage' }).querySelector('img')
  ).toHaveAttribute('src', expectedIcon);
  expect(screen.queryByRole('heading', { name: /vs\. Illidan/ })).not.toBeInTheDocument();
  expect(screen.getByRole('button', { name: /Retribution Paladin/ })).toBeInTheDocument();
});
