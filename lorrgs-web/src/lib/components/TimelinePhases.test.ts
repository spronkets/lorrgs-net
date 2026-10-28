import { render, screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';
import TimelinePhases from './TimelinePhases.svelte';

describe('TimelinePhases', () => {
  it('distinguishes corrected intermissions from numbered phases', () => {
    render(TimelinePhases, {
      phases: [{ id: 1.75, timestamp: 45000, isIntermission: true }],
      axisDuration: 100000
    });
    expect(screen.getByRole('img', { name: 'Intermission 1.75 at 0:45' })).toHaveTextContent(
      'I1.75'
    );
  });
  it('positions and labels observed transitions on the shared elapsed-time axis', async () => {
    const { rerender } = render(TimelinePhases, {
      phases: [
        { id: 2, timestamp: 60000 },
        { id: 3, timestamp: 90000 }
      ],
      axisDuration: 300000
    });
    expect(screen.getByRole('img', { name: 'Phase 2 at 1:00' })).toHaveStyle({ left: '20%' });
    expect(screen.getByRole('img', { name: 'Phase 3 at 1:30' })).toHaveStyle({ left: '30%' });
    await rerender({ phases: [], axisDuration: 300000 });
    expect(screen.queryByRole('img')).not.toBeInTheDocument();
  });
});
