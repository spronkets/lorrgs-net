import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import BossTimelineRow from './BossTimelineRow.svelte';
import { getBossTimeline } from '../api';
import type { BossTimeline } from '../models/BossTimeline';
import { bossResponse, rows } from '../testing/timelineFixtures';

vi.mock('../api', () => ({ getBossTimeline: vi.fn() }));
const props = () => ({
  report: rows[0].report,
  edition: 'anniversary',
  axisDuration: 300000,
  ticks: [0, 30000],
  showIcons: true,
  showCasts: true
});
beforeEach(() => {
  vi.mocked(getBossTimeline).mockReset();
  vi.mocked(getBossTimeline).mockImplementation(async (report) => bossResponse(report));
});

describe('BossTimelineRow', () => {
  it('renders bundled abilities, observed phases and the reference end on the shared time axis', async () => {
    const input = props();
    const { container, rerender } = render(BossTimelineRow, {
      ...input,
      bossSlug: 'illidan-stormrage',
      bossIconName: 'achievement_boss_illidan.jpg',
      report: { ...input.report, bossTimeline: bossResponse(input.report) }
    });
    await screen.findByRole('button', { name: '0:30 · Boss Strike' });
    expect(screen.queryByText(/^Reference:/)).not.toBeInTheDocument();
    expect(screen.getByRole('img', { name: 'Phase 2 at 1:00' })).toHaveStyle({ left: '20%' });
    expect(screen.getByRole('img', { name: 'Reference fight end at 4:00' })).toBeInTheDocument();
    expect(container.querySelector('.boss-event')?.getAttribute('style')).toContain('10%');
    expect(container.querySelector('.boss-event img')).not.toBeNull();
    expect(screen.getByRole('link', { name: 'Boss' }).querySelector('.boss-icon')).toHaveAttribute(
      'src',
      'https://wow.zamimg.com/images/wow/icons/medium/achievement_boss_illidan.jpg'
    );
    expect(getBossTimeline).not.toHaveBeenCalled();
    await rerender({
      ...input,
      report: { ...input.report, bossTimeline: bossResponse(input.report) },
      showCasts: false
    });
    expect(screen.queryByRole('button', { name: '0:30 · Boss Strike' })).not.toBeInTheDocument();
    expect(screen.getByRole('img', { name: 'Phase 2 at 1:00' })).toBeInTheDocument();
  });

  it('cancels the old reference on sorting and ignores its late response', async () => {
    let resolve!: (timeline: BossTimeline) => void;
    vi.mocked(getBossTimeline).mockImplementationOnce(
      () =>
        new Promise((done) => {
          resolve = done;
        })
    );
    const { rerender, unmount } = render(BossTimelineRow, props());
    await waitFor(() => expect(getBossTimeline).toHaveBeenCalledTimes(1));
    const signal = vi.mocked(getBossTimeline).mock.calls[0][2]!;
    await rerender({ ...props(), report: rows[4].report });
    await screen.findByRole('button', { name: '0:30 · Boss Strike' });
    expect(signal.aborted).toBe(true);
    resolve({ ...bossResponse(rows[0].report), name: 'Stale boss' });
    await Promise.resolve();
    expect(screen.queryByText('Stale boss')).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Boss' })).toHaveAttribute(
      'href',
      'https://fresh.warcraftlogs.com/reports/Report4#fight=5'
    );
    unmount();
    expect(vi.mocked(getBossTimeline).mock.calls[1][2]?.aborted).toBe(true);
  });

  it('shows loading and errors, retries explicitly and marks incomplete or empty timelines', async () => {
    const logged = vi.spyOn(console, 'error').mockImplementation(() => {});
    vi.mocked(getBossTimeline).mockRejectedValueOnce(new Error('Rate limited'));
    try {
      const { rerender } = render(BossTimelineRow, props());
      const notice = await screen.findByText('Rate limited');
      expect(notice.closest('.alert')).toHaveAttribute('data-variant', 'warning');
      expect(screen.getByText('Rate limited')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Retry boss timeline' })).toHaveAttribute('data-tone', 'warning');
      expect(screen.getByRole('button', { name: 'Retry boss timeline' })).toHaveAttribute('data-outline', 'true');
      await fireEvent.click(screen.getByRole('button', { name: 'Retry boss timeline' }));
      await screen.findByRole('button', { name: '0:30 · Boss Strike' });
      await rerender({
        ...props(),
        report: { ...rows[0].report, bossTimeline: { ...bossResponse(rows[0].report), events: [] } }
      });
      await screen.findByText('No enemy casts were recorded for this fight.');
      vi.mocked(getBossTimeline).mockResolvedValueOnce({
        ...bossResponse(rows[4].report),
        isComplete: false
      });
      await rerender({ ...props(), report: rows[4].report });
      await screen.findByText('Partial boss timeline — event limit reached');
    } finally {
      logged.mockRestore();
    }
  });
});
