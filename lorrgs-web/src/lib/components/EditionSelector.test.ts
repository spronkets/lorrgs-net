import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';
import EditionSelector from './EditionSelector.svelte';

const versions = ['Anniversary', 'Retail'];

describe('EditionSelector', () => {
  it('renders edition cards, marks the active choice, resolves icons, and selects a different card', async () => {
    const getVersionIconUrl = vi.fn((version: string) => `/${version.toLowerCase()}.svg`);
    const onselect = vi.fn();
    const { container } = render(EditionSelector, {
      versions,
      selectedVersion: 'Anniversary',
      getVersionIconUrl,
      onselect
    });

    const cards = screen.getAllByRole('button');
    expect(cards).toHaveLength(2);
    expect(cards[0]).toHaveClass('active');
    expect(cards[1]).not.toHaveClass('active');
    expect(container.querySelectorAll('img')).toHaveLength(4);
    expect(container.querySelector('img[alt="Anniversary"]')).toHaveAttribute(
      'src',
      '/anniversary.svg'
    );
    expect(getVersionIconUrl).toHaveBeenCalledTimes(4);

    await fireEvent.click(cards[1]);
    expect(onselect).toHaveBeenCalledWith('Retail');
    expect(onselect).toHaveBeenCalledTimes(1);
    await fireEvent.click(cards[0]);
    expect(onselect).toHaveBeenCalledTimes(1);
  });

  it('renders the select variant with a placeholder and ignores blank or already selected values', async () => {
    const onselect = vi.fn();
    const { rerender } = render(EditionSelector, {
      versions,
      selectedVersion: '',
      getVersionIconUrl: (version: string) => `/${version}.svg`,
      variant: 'select',
      placeholder: 'Choose a version',
      onselect
    });
    const select = screen.getByRole('combobox');
    expect(select).toHaveValue('');
    expect(screen.getByRole('option', { name: 'Choose a version' })).toHaveValue('');
    expect(screen.getAllByRole('option')).toHaveLength(3);

    await fireEvent.change(select, { target: { value: '' } });
    expect(onselect).not.toHaveBeenCalled();
    await fireEvent.change(select, { target: { value: 'Retail' } });
    expect(onselect).toHaveBeenCalledWith('Retail');
    await rerender({
      versions,
      selectedVersion: 'Retail',
      getVersionIconUrl: (version: string) => `/${version}.svg`,
      variant: 'select',
      onselect
    });
    await fireEvent.change(screen.getByRole('combobox'), { target: { value: 'Retail' } });
    expect(onselect).toHaveBeenCalledTimes(1);
  });

  it('initializes the select to the controlled edition and sends a replacement selection', async () => {
    const onselect = vi.fn();
    render(EditionSelector, {
      versions,
      selectedVersion: 'Retail',
      getVersionIconUrl: (version: string) => `/${version}.svg`,
      variant: 'select',
      onselect
    });
    const select = screen.getByRole('combobox');
    expect(select).toHaveValue('Retail');
    await fireEvent.change(select, { target: { value: 'Anniversary' } });
    expect(onselect).toHaveBeenCalledExactlyOnceWith('Anniversary');
  });

  it('leaves no edition selected when the controlled value is absent from the available options', () => {
    render(EditionSelector, {
      versions,
      selectedVersion: 'Archived edition',
      getVersionIconUrl: (version: string) => `/${version}.svg`,
      variant: 'select'
    });
    expect((screen.getByRole('combobox') as HTMLSelectElement).selectedIndex).toBe(-1);
  });

  it('supports empty card lists and optional callbacks', async () => {
    const { container, rerender } = render(EditionSelector, {
      versions: [],
      selectedVersion: '',
      getVersionIconUrl: () => '/unused.svg'
    });
    expect(container.querySelectorAll('button')).toHaveLength(0);
    await rerender({
      versions: [],
      selectedVersion: '',
      getVersionIconUrl: () => '/unused.svg',
      variant: 'select'
    });
    expect(screen.getAllByRole('option')).toHaveLength(1);
    await rerender({
      versions: ['Era'],
      selectedVersion: '',
      getVersionIconUrl: () => '/era.svg',
      variant: 'select'
    });
    await fireEvent.change(screen.getByRole('combobox'), { target: { value: 'Era' } });

    await rerender({
      versions: [''],
      selectedVersion: '',
      getVersionIconUrl: () => '/empty.svg',
      variant: 'select'
    });
    expect(screen.getAllByRole('option')).toHaveLength(2);
    await fireEvent.change(screen.getByRole('combobox'), { target: { value: '' } });
  });
});
