import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';
import CopyNoteDialog from './CopyNoteDialog.svelte';
import { rows, response } from '../testing/timelineFixtures';

const report = {
  ...rows[0].report,
  fights: [
    {
      ...rows[0].report.fights[0],
      encounterId: 3455,
      phases: [
        { id: 1, timestamp: 0 },
        { id: 2, timestamp: 5000 }
      ]
    }
  ]
};
const props = {
  report,
  timeline: response(report),
  hidden: new Set<number>(),
  difficulty: 'mythic',
  bossName: 'Vashnik',
  specSlug: 'retribution-paladin',
  onclose: vi.fn(),
  oncopied: vi.fn()
};
afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe('copy note editor', () => {
  it('opens a modal with live formats, nickname, spec targeting and phase timing', async () => {
    render(CopyNoteDialog, props);
    expect(screen.getByRole('dialog', { name: 'Copy Note' })).toHaveAttribute('open');
    const preview = screen.getByRole('textbox', { name: 'Note preview' });
    expect(preview).toHaveClass('monospace');
    expect(preview).toHaveStyle('min-height: 260px');
    expect(preview).toHaveValue('{time:00:10.0} - {spell:31884}');
    await fireEvent.input(screen.getByRole('textbox', { name: 'Name or Nickname' }), {
      target: { value: 'Me' }
    });
    expect(preview).toHaveValue('{time:00:10.0} - Me {spell:31884}');
    await fireEvent.click(screen.getByRole('button', { name: 'NSRT' }));
    expect(preview).toHaveValue(
      'EncounterID:3455;Difficulty:Mythic;Name:Vashnik;\nph:1;time:10.0;tag:Me;spellid:31884;'
    );
    await fireEvent.change(screen.getByRole('combobox', { name: 'Target' }), {
      target: { value: 'spec' }
    });
    expect(screen.getByRole('textbox', { name: 'Spec tag' })).toHaveValue('70');
    await fireEvent.click(screen.getByRole('checkbox', { name: 'Phase-relative timers' }));
    expect(preview).toHaveValue(
      'EncounterID:3455;Difficulty:Mythic;Name:Vashnik;\nph:2;time:5.0;tag:70;spellid:31884;'
    );
    await fireEvent.click(screen.getByRole('button', { name: 'Close copy note' }));
    expect(props.onclose).toHaveBeenCalled();
  });

  it('reports partial timelines and empty selections and disables unavailable dynamic timing', () => {
    render(CopyNoteDialog, {
      ...props,
      hidden: new Set([31884]),
      timeline: { ...props.timeline, isComplete: false },
      report: { ...report, fights: [{ ...report.fights[0], phases: [] }] }
    });
    expect(screen.getByText(/Partial timeline/)).toBeVisible();
    expect(screen.getByRole('alert')).toHaveTextContent('No selected cast events');
    expect(screen.getByRole('button', { name: 'Copy to clipboard' })).toBeDisabled();
    expect(screen.getByRole('checkbox', { name: 'Phase-relative timers' })).toBeDisabled();
  });

  it('copies the exact preview only on request', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    vi.stubGlobal('navigator', { clipboard: { writeText } });
    const oncopied = vi.fn();
    render(CopyNoteDialog, { ...props, oncopied });
    expect(writeText).not.toHaveBeenCalled();
    await fireEvent.click(screen.getByRole('button', { name: 'Copy to clipboard' }));
    await waitFor(() => expect(oncopied).toHaveBeenCalledWith('Player0'));
    expect(writeText).toHaveBeenCalledWith('{time:00:10.0} - {spell:31884}');
  });

  it('keeps the preview available and surfaces clipboard errors for manual copying', async () => {
    const error = new Error('Permission denied');
    const log = vi.spyOn(console, 'error').mockImplementation(() => {});
    vi.stubGlobal('navigator', { clipboard: { writeText: vi.fn().mockRejectedValue(error) } });
    const oncopied = vi.fn();
    render(CopyNoteDialog, { ...props, oncopied });
    await fireEvent.click(screen.getByRole('button', { name: 'Copy to clipboard' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Select the preview and copy it manually'
    );
    expect(log).toHaveBeenCalledWith('Unable to copy raid note', error);
    expect(oncopied).not.toHaveBeenCalled();
    expect(screen.getByRole('textbox', { name: 'Note preview' })).toBeVisible();
  });

  it('uses the matching boss timeline, falls back to the report player, and shows unavailable spec tags', async () => {
    const reportWithoutSpecPlayer = {
      ...report,
      players: [
        {
          name: 'Player0',
          playerId: 7,
          classId: 2,
          specId: 65,
          specSlug: 'other-spec',
          performance: 100
        }
      ],
      bossTimeline: {
        fightId: report.fights![0].fightId,
        phases: [{ id: 2, timestamp: 0 }]
      }
    };
    const timeline = { ...props.timeline, playerName: 'Player0' };
    const { unmount } = render(CopyNoteDialog, {
      ...props,
      report: reportWithoutSpecPlayer,
      timeline,
      bossName: ''
    });

    expect(screen.getByText('Player0 · Boss')).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: 'Phase-relative timers' })).toBeDisabled();
    await fireEvent.change(screen.getByRole('combobox', { name: 'Target' }), {
      target: { value: 'spec' }
    });
    expect(screen.getByRole('textbox', { name: 'Spec tag' })).toHaveValue('spec:PALADIN:1');
    await fireEvent.click(screen.getByRole('button', { name: 'NSRT' }));
    expect(screen.getByRole('textbox', { name: 'Spec tag' })).toHaveValue('65');

    unmount();
    render(CopyNoteDialog, {
      ...props,
      report: { ...report, players: [] },
      timeline: { ...props.timeline, playerName: 'Unlisted player' }
    });
    await fireEvent.change(screen.getByRole('combobox', { name: 'Target' }), {
      target: { value: 'spec' }
    });
    expect(screen.getByRole('textbox', { name: 'Spec tag' })).toHaveValue('Unavailable');
  });
});
