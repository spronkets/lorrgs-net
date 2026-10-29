import { fireEvent, render, screen, within } from '@testing-library/svelte';
import { expect, it } from 'vitest';
import WorldDataPage from './WorldDataPage.svelte';

it('renders each world-data category, including optional metadata fallbacks', async () => {
  const { container } = render(WorldDataPage, {
    worldData: {
      classes: [
        { id: 2, name: 'Paladin', nameSlug: 'paladin', color: '#f58cba', maskId: 2 },
        { id: 99, name: 'Unknown', nameSlug: 'unmapped-class', color: '#123456', maskId: 99 }
      ],
      specs: [
        {
          id: 65,
          name: 'Holy',
          nameSlug: 'holy',
          fullName: 'Holy Paladin',
          fullNameSlug: 'holy-paladin',
          classId: 2,
          roleId: 2
        },
        {
          id: 999,
          name: 'Unknown',
          nameSlug: 'unknown',
          fullName: 'Unknown Spec',
          fullNameSlug: 'unknown-spec',
          classId: 404,
          roleId: 0
        }
      ],
      roles: [
        { id: 2, name: 'Healer', nameSlug: 'healer', code: 'heal', color: '#fff', metric: 'hps' },
        { id: 3, name: 'Unspecified', nameSlug: 'unspecified', code: 'none', color: '#aaa' }
      ],
      bosses: [
        { id: 50609, name: 'Illidan', nameSlug: 'illidan', icon: 'illidan.jpg', raidId: 1060 }
      ],
      zones: [
        { id: 1, name: 'Black Temple', slug: 'black-temple', bossIds: [50609] },
        { id: 2, name: 'Empty zone', slug: 'empty-zone', bossIds: [] },
        { id: 3, name: 'Unknown zone', slug: 'unknown-zone' }
      ],
      spells: [{ id: 31821, name: 'Aura Mastery', icon: 'spell_holy_auramastery', type: 'Buff' }],
      trinkets: [
        { id: 12345, name: 'Test Trinket', icon: 'inv_jewelry_trinket_01', itemLevel: 245 }
      ],
      seasons: [
        { name: 'Season One', slug: 'season-one', startDate: '2026-01-02T00:00:00Z' },
        { name: 'Undated season', slug: 'undated' }
      ]
    }
  });

  expect(screen.getByRole('heading', { name: 'World Data' })).toBeInTheDocument();
  expect(screen.getByRole('heading', { name: 'Paladin' })).toBeInTheDocument();
  expect(screen.getByRole('heading', { name: 'Unknown' })).toBeInTheDocument();
  const classIcon = screen.getByRole('img', { name: 'Paladin' });
  await fireEvent.error(classIcon);
  expect(classIcon).toHaveStyle({ display: 'none' });

  await fireEvent.click(screen.getByRole('button', { name: 'Specs' }));
  expect(screen.getByRole('heading', { name: 'Holy Paladin' })).toBeInTheDocument();
  expect(screen.getByRole('heading', { name: 'Unknown Spec' })).toBeInTheDocument();
  expect(screen.getByText('Paladin', { selector: '.meta' })).toBeInTheDocument();
  const specImages = container.querySelectorAll('.entry-icon');
  expect(specImages[0]).toHaveAttribute('src', '/images/wow/specs/paladin-holy.jpg');
  expect(specImages[1]).toHaveAttribute('src', '/images/wow/specs/other-unknown.jpg');
  await fireEvent.error(specImages[1]);
  expect(specImages[1]).toHaveStyle({ display: 'none' });

  await fireEvent.click(screen.getByRole('button', { name: 'Roles' }));
  expect(screen.getByText('Metric: HPS')).toBeInTheDocument();
  expect(screen.getByText('Metric: N/A')).toBeInTheDocument();

  await fireEvent.click(screen.getByRole('button', { name: 'Bosses' }));
  expect(screen.getByText('ID: 50609')).toBeInTheDocument();
  expect(screen.getByRole('heading', { name: 'Illidan' })).toBeInTheDocument();

  await fireEvent.click(screen.getByRole('button', { name: 'Zones' }));
  expect(screen.getByText('1 bosses')).toBeInTheDocument();
  expect(screen.getAllByText('0 bosses')).toHaveLength(2);

  await fireEvent.click(screen.getByRole('button', { name: 'Spells' }));
  expect(screen.getByRole('heading', { name: 'Aura Mastery' })).toBeInTheDocument();
  expect(screen.getByText('Buff')).toBeInTheDocument();
  await fireEvent.error(screen.getByRole('img', { name: 'Aura Mastery' }));

  await fireEvent.click(screen.getByRole('button', { name: 'Trinkets' }));
  expect(screen.getByRole('heading', { name: 'Test Trinket' })).toBeInTheDocument();
  expect(screen.getByText('iLvl 245')).toBeInTheDocument();
  await fireEvent.error(screen.getByRole('img', { name: 'Test Trinket' }));

  await fireEvent.click(screen.getByRole('button', { name: 'Seasons' }));
  expect(screen.getAllByText(/Started:/)).toHaveLength(2);
  expect(screen.getAllByText(/Started:/)[1]).toHaveTextContent('N/A');
  expect(within(container).getByText('season-one')).toBeInTheDocument();
});

it('shows empty grids when the snapshot has no category data', async () => {
  const { container } = render(WorldDataPage, { worldData: {} });

  for (const tab of [
    'Classes',
    'Specs',
    'Roles',
    'Bosses',
    'Zones',
    'Spells',
    'Trinkets',
    'Seasons'
  ]) {
    await fireEvent.click(screen.getByRole('button', { name: tab }));
    expect(container.querySelectorAll('.data-grid .data-item')).toHaveLength(0);
  }
});
