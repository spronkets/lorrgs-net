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
  it('omits the rank column in composition mode so boss and player columns align', () => {
    const input = props();
    const { container } = render(BossTimelineRow, {
      ...input, compositionMode: true,
      report: { ...input.report, bossTimeline: bossResponse(input.report) }
    });
    expect(container.querySelector('.position-placeholder')).toBeNull();
    expect(container.querySelector('.boss-row')?.firstElementChild).toHaveClass('identity');
  });
  it('filters received effects with the same controls and lower lanes as player timelines', async () => {
    const input = props();
    const boss = bossResponse(input.report);
    boss.spells.push({ id: 3, name: 'Received effect', icon: null });
    boss.events.push(
      { spellId: 3, timestamp: 1000, duration: 5000, kind: 'buff', sourceName: 'Healer' },
      { spellId: 3, timestamp: 2000, duration: null, kind: 'damage', amount: 100 },
      { spellId: 3, timestamp: 3000, duration: null, kind: 'heal', amount: 50 },
      { spellId: 3, timestamp: 4000, duration: null, kind: 'hot', amount: 10 },
      { spellId: 3, timestamp: 5000, duration: null, kind: 'mana-returned', amount: 20 },
      { spellId: 0, timestamp: 6000, duration: null, kind: 'death' }
    );
    const base = { ...input, report: { ...input.report, bossTimeline: boss }, showBuffs: false, showDeaths: false };
    const { container, rerender } = render(BossTimelineRow, base);
    expect(container.querySelectorAll('.boss-event')).toHaveLength(1);
    await rerender({ ...base, showBuffs: true, showReceivedBuffs: true, showDamageTaken: true,
      showDirectHeals: true, showHots: true, showManaReceived: true, showDeaths: true });
    expect(container.querySelectorAll('.boss-event')).toHaveLength(7);
    expect(container.querySelector('.track')).toHaveStyle({ height: '108px' });
    expect(container.querySelectorAll('.cast-connectors')).toHaveLength(6);
    expect(container.querySelectorAll('.cast-connectors.below')).toHaveLength(5);
    expect(container.querySelector('.cast-connectors.below')).toHaveStyle({ top: '47px' });
    expect(container.querySelector('.cast-window')).not.toBeNull();
    for (const event of container.querySelectorAll('[data-event-index="1"], [data-event-index="2"], [data-event-index="3"], [data-event-index="4"], [data-event-index="5"]'))
      expect(event).toHaveStyle({ top: '75px' });
    await rerender({ ...base, showCasts: false, showReceivedBuffs: false, showDamageTaken: false,
      showDirectHeals: false, showHots: false, showManaReceived: false });
    expect(container.querySelectorAll('.boss-event')).toHaveLength(0);
    expect(container.querySelector('.track')).toHaveStyle({ height: '68px' });
  });

  it('renders bundled abilities, observed phases and the reference end on the shared time axis', async () => {
    const input = props();
    const { container, rerender } = render(BossTimelineRow, {
      ...input,
      bossSlug: 'illidan-stormrage',
      bossIconName: 'achievement_boss_illidan.jpg',
      report: { ...input.report, bossTimeline: bossResponse(input.report) }
    });
    await screen.findByRole('button', { name: '0:30 · Boss Strike' });
    expect(container.querySelector('.position-placeholder .lucide-skull')).toHaveAttribute(
      'aria-hidden', 'true'
    );
    expect(screen.queryByText(/^Reference:/)).not.toBeInTheDocument();
    expect(screen.getByRole('img', { name: 'Phase 2 at 1:00' })).toHaveStyle({ left: '20%' });
    expect(screen.getByRole('img', { name: 'Reference fight end at 4:00' })).toBeInTheDocument();
    expect(container.querySelector('.boss-event')?.getAttribute('style')).toContain('10%');
    expect(container.querySelector('.boss-event img')).not.toBeNull();
    expect(container.querySelector('.boss-heading .boss-icon')).toHaveAttribute(
      'src',
      'https://wow.zamimg.com/images/wow/icons/medium/achievement_boss_illidan.jpg'
    );
    expect(screen.getByText('Boss').closest('a')).toBeNull();
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
    expect(screen.getByText('Boss')).toBeInTheDocument();
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
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
      await screen.findByText('No enemy events were recorded for this fight.');
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
