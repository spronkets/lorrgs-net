import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';
import BossIcon from './BossIcon.svelte';

describe('BossIcon', () => {
  it('generates varied decorative rays that remain stable for the same boss', async () => {
    const { container, rerender } = render(BossIcon, { slug: 'halazzi' });
    const rays = container.querySelector('.boss-rays')!;
    expect(rays).toHaveAttribute('aria-hidden', 'true');
    const gradient = rays.querySelector('radialGradient')!;
    expect(gradient).toHaveAttribute('gradientUnits', 'userSpaceOnUse');
    expect(gradient.querySelector('stop')).toHaveAttribute('stop-color', 'white');
    expect(rays.querySelector('.ray-fan')).toHaveAttribute('fill', `url(#${gradient.id})`);
    expect(rays.querySelectorAll('.ray-fan')).toHaveLength(9);
    expect(rays.querySelectorAll('.ray-highlight')).toHaveLength(0);
    expect(rays.querySelector('.dark-rings')).not.toBeNull();
    expect(rays.querySelector('.light-rings')).not.toBeNull();
    const spirals = rays.querySelectorAll('.light-rings path');
    expect(spirals).toHaveLength(3);
    for (const spiral of spirals) {
      const path = spiral.getAttribute('d')!;
      expect(path.split(' ')).toHaveLength(65);
      const [x, y] = path.split(' ').at(-1)!.slice(1).split(',').map(Number);
      expect(x < 0 || x > 100 || y - 5 < 0 || y - 5 > 100).toBe(true);
    }
    expect(rays.nextElementSibling).toHaveClass('boss-icon');
    const points = Array.from(rays.querySelectorAll('.ray-fan')).map((ray) =>
      ray.getAttribute('points')
    );
    expect(points).toHaveLength(9);
    expect(new Set(points).size).toBe(9);
    for (const fan of points) {
      const [x, y] = fan!.split(' ')[3].split(',').map(Number);
      expect(x < 0 || x > 100 || y - 5 < 0 || y - 5 > 100).toBe(true);
    }
    const lengths = points.map((points) => {
      const [x, y] = points!.split(' ')[1].split(',').map(Number);
      return Math.hypot(x - 50, y - 55);
    });
    expect(new Set(lengths).size).toBeGreaterThan(1);
    const widths = points.map((points) => {
      const vertices = points!.split(' ').map((point) => point.split(',').map(Number));
      const first = Math.atan2(vertices[1][1] - 55, vertices[1][0] - 50);
      const last = Math.atan2(vertices[5][1] - 55, vertices[5][0] - 50);
      return (last - first + Math.PI * 2) % (Math.PI * 2);
    });
    expect(new Set(widths).size).toBeGreaterThan(1);
    expect(Math.min(...widths)).toBeGreaterThan(0.08);
    expect(Math.max(...widths)).toBeLessThan(0.55);
    await rerender({ slug: 'halazzi', size: 40 });
    expect(
      Array.from(container.querySelectorAll('.ray-fan')).map((ray) => ray.getAttribute('points'))
    ).toEqual(points);
    await rerender({ slug: 'supremus' });
    expect(container.querySelector('polygon')?.getAttribute('points')).not.toBe(points[0]);
  });

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
