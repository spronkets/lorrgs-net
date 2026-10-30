import { render, screen } from '@testing-library/svelte';
import { expect, it } from 'vitest';
import ReportIdentity from './ReportIdentity.svelte';

it.each([1, 2, null])('renders shared guild/report identity with faction %s', (factionId) => {
  const { container } = render(ReportIdentity, {
    edition: 'anniversary',
    report: {
      reportId: 'ReportA', title: 'First Monday ZA', guildName: 'Aftershock',
      factionId, serverRegion: 'eu', serverName: 'Thunderstrike'
    }
  });
  expect(screen.getByRole('link', { name: 'Aftershock EU' })).toHaveAttribute(
    'href', 'https://fresh.warcraftlogs.com/reports/ReportA'
  );
  expect(screen.getByText('(First Monday ZA)')).toBeInTheDocument();
  expect(screen.getByRole('link')).toHaveTextContent('Aftershock EU');
  expect(screen.getByText('Thunderstrike')).toBeInTheDocument();
  expect(container.querySelector('img.faction-icon')).toBeNull();
  if (factionId !== null) {
    const faction = factionId === 1 ? 'alliance' : 'horde';
    expect(screen.getByRole('link')).toHaveClass('faction-name', faction);
    expect(screen.getByRole('link')).toHaveAttribute('title', factionId === 1 ? 'Alliance' : 'Horde');
    expect(screen.getByText('Aftershock EU')).not.toHaveClass('faction-name');
  } else {
    expect(screen.getByRole('link')).not.toHaveClass('alliance', 'horde');
  }
});

it('uses the report title for personal reports without inventing guild or realm metadata', () => {
  render(ReportIdentity, {
    edition: 'era', report: { reportId: 'ReportA', title: 'Personal raid' }
  });
  expect(screen.getByRole('link')).toHaveTextContent('Personal raid');
  expect(screen.queryByText('(Personal raid)')).not.toBeInTheDocument();
});

it('can omit the report subtitle until composition details are loaded', () => {
  render(ReportIdentity, {
    edition: 'anniversary', showReportName: false,
    report: { reportId: 'ReportA', title: 'Raid', guildName: 'Guild' }
  });

  it('places the unparenthesized composition report name before the link icon', () => {
    render(ReportIdentity, {
      edition: 'anniversary',
      reportNameInLink: true,
      report: { reportId: 'ReportA', title: 'Monday raid', guildName: 'Aftershock' }
    });
    const link = screen.getByRole('link', { name: 'Monday raid' });
    expect(link).toHaveTextContent('Monday raid');
    expect(link).not.toContainElement(screen.getByText('Aftershock'));
    expect(link.lastElementChild).toHaveClass('lucide-external-link');
    expect(screen.queryByText('(Monday raid)')).not.toBeInTheDocument();
  });
  expect(screen.queryByText('(Raid)')).not.toBeInTheDocument();
});

it.each([1, 2, null])('can place region beside the server without an icon for faction %s', (factionId) => {
  render(ReportIdentity, {
    edition: 'anniversary',
    regionNextToServer: true,
    report: {
      reportId: 'ReportA', title: 'Raid', guildName: 'Aftershock',
      factionId, serverRegion: 'eu', serverName: 'Thunderstrike'
    }
  });
  expect(screen.getByRole('link', { name: 'Aftershock' })).toBeInTheDocument();
  const region = screen.getByText('Thunderstrike EU');
  expect(region).not.toHaveClass('faction-name', 'alliance', 'horde');
  expect(region).toHaveTextContent('Thunderstrike EU');
});
