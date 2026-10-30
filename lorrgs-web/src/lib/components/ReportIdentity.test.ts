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
  expect(screen.getByRole('link', { name: 'Aftershock' })).toHaveAttribute(
    'href', 'https://fresh.warcraftlogs.com/reports/ReportA'
  );
  expect(screen.getByText('(First Monday ZA)')).toBeInTheDocument();
  expect(screen.getByText('EU')).toBeInTheDocument();
  expect(screen.getByText('Thunderstrike')).toBeInTheDocument();
  expect(container.querySelector('.faction-icon') !== null).toBe(factionId !== null);
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
  expect(screen.queryByText('(Raid)')).not.toBeInTheDocument();
});
