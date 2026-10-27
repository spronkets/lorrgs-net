import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';
import BossIcon from './BossIcon.svelte';

describe('BossIcon', () => {
  it('detects an accent after image loading and resets it when the portrait changes', async () => {
    const original = Object.getOwnPropertyDescriptor(HTMLCanvasElement.prototype, 'getContext')!;
    Object.defineProperty(HTMLCanvasElement.prototype, 'getContext', {
      configurable: true,
      value: () => ({
        drawImage: vi.fn(),
        getImageData: () => ({ data: new Uint8ClampedArray([192, 128, 32, 255]) })
      })
    });
    try {
      const { container, rerender } = render(BossIcon, { slug: 'supremus' });
      const image = container.querySelector('img')!;
      expect(image).toHaveAttribute('crossorigin', 'anonymous');
      await fireEvent.load(image);
      expect(container.querySelector('.boss-portrait')?.getAttribute('style')).toContain(
        '--boss-accent: rgb(192, 128, 32)'
      );
      await rerender({ slug: 'illidan-stormrage' });
      expect(container.querySelector('.boss-portrait')?.getAttribute('style')).toContain(
        '--boss-accent: #7b8995'
      );
    } finally {
      Object.defineProperty(HTMLCanvasElement.prototype, 'getContext', original);
    }
  });

  it('frames and crops journal portraits at the requested square size', () => {
    const { container } = render(BossIcon, {
      slug: 'the-illidari-council',
      alt: 'The Illidari Council',
      size: 40
    });
    const image = screen.getByRole('img', { name: 'The Illidari Council' });
    expect(image).toHaveClass('boss-icon');
    expect(image.parentElement).toHaveClass('boss-portrait', 'journal');
    expect(container.querySelector('.boss-portrait')).toHaveStyle({
      width: '40px',
      height: '40px'
    });
  });

  it('keeps square icon artwork decorative by default', () => {
    const { container } = render(BossIcon, {
      slug: 'illidan-stormrage',
      iconName: 'achievement_boss_illidan.jpg'
    });
    expect(container.querySelector('img')).toHaveAttribute(
      'src',
      'https://wow.zamimg.com/images/wow/icons/medium/achievement_boss_illidan.jpg'
    );
    expect(container.querySelector('img')).toHaveAttribute('alt', '');
    expect(container.querySelector('.boss-portrait')).not.toHaveClass('journal');
    expect(container.querySelector('.boss-portrait')).toHaveStyle({
      width: '30px',
      height: '30px'
    });
  });

  it('omits missing and failed images and recovers when the source changes', async () => {
    const error = vi.spyOn(console, 'error').mockImplementation(() => {});
    const { container, rerender } = render(BossIcon, { slug: 'unknown' });
    expect(container.querySelector('.boss-portrait')).toBeNull();
    await rerender({ slug: 'unknown', iconName: 'missing' });
    await fireEvent.error(container.querySelector('img')!);
    expect(error).toHaveBeenCalledWith('Unable to load boss icon', {
      src: 'https://wow.zamimg.com/images/wow/icons/medium/missing.jpg'
    });
    expect(container.querySelector('.boss-portrait')).toBeNull();
    await rerender({ slug: 'supremus', iconName: null });
    expect(container.querySelector('img')).toHaveAttribute(
      'src',
      'https://wow.zamimg.com/images/wow/journal/ui-ej-boss-supremus.png'
    );
    error.mockRestore();
  });
});
