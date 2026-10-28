import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';
import App from './App.svelte';
import * as API from './lib/api';
import { getEditionSlug } from './lib/raidCatalog';
import { getVersionIconUrl } from './lib/selectionIcons';

describe('App version mapping', () => {
  it('uses the retail catalog key and retail icon for the Retail option', () => {
    expect(getEditionSlug('Retail')).toBe('retail');
    expect(getVersionIconUrl('Retail')).toBe('/images/wow/editions/midnight.png');
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
    localStorage.clear();
    history.replaceState({}, '', '/');
  });

  it('keeps navigation compact, closes Tools on selection and separates theme from the brand', async () => {
    vi.spyOn(API, 'waitForHealthReady').mockImplementation(() => new Promise<boolean>(() => {}));
    localStorage.setItem('lorrgs-theme', 'light');
    render(App);
    const nav = screen.getByRole('navigation', { name: 'Main navigation' });
    expect(nav).not.toHaveTextContent('Warcraft Logs rankings');
    expect(screen.getByText('lorrgs.io').closest('footer')).not.toBeNull();
    const tools = screen.getByText('Tools').closest('details')!;
    expect(tools).not.toHaveAttribute('open');
    await fireEvent.click(screen.getByText('Tools'));
    expect(tools).toHaveAttribute('open');
    await fireEvent.click(screen.getByRole('button', { name: 'World Data' }));
    expect(tools).not.toHaveAttribute('open');
    await waitFor(() => expect(new URLSearchParams(location.search).get('page')).toBe('worlddata'));
    await fireEvent.click(screen.getByRole('button', { name: 'Switch to dark mode' }));
    expect(localStorage.getItem('lorrgs-theme')).toBe('dark');
    expect(new URLSearchParams(location.search).get('page')).toBe('worlddata');
    await fireEvent.click(screen.getByRole('button', { name: 'LorrgsNET home' }));
    await waitFor(() => expect(new URLSearchParams(location.search).has('page')).toBe(false));
    expect(localStorage.getItem('lorrgs-theme')).toBe('dark');
    await fireEvent.click(screen.getByText('Tools'));
    await fireEvent.keyDown(window, { key: 'Escape' });
    expect(tools).not.toHaveAttribute('open');
  });
});
