import { describe, expect, it } from 'vitest';
import { detectPortraitColor, fitPortraitArtwork } from './portraitColor';

it('fits and centers visible artwork rather than transparent image padding', () => {
  const pixels = new Uint8ClampedArray(128 * 64 * 4);
  for (let y = 2; y <= 62; y++) {
    for (let x = 10; x <= 97; x++) pixels[(y * 128 + x) * 4 + 3] = 255;
  }
  const style = fitPortraitArtwork(pixels, 128, 64);
  const values = Object.fromEntries(style.split('; ').map((part) => part.split(': ')));
  const scale = parseFloat(values.width) / 128;
  expect(parseFloat(values.left) + 10 * scale).toBeCloseTo(0);
  expect(parseFloat(values.left) + 98 * scale).toBeCloseTo(100);
  expect(parseFloat(values.top) + 32.5 * scale).toBeCloseTo(50);
  expect(fitPortraitArtwork(new Uint8ClampedArray(16), 2, 2)).toBe('');
});

describe('portrait color detection', () => {
  it('detects the dominant artwork color instead of transparent or black backgrounds', () => {
    const pixels = new Uint8ClampedArray([
      ...Array(20).fill([0, 0, 0, 0]).flat(),
      ...Array(20).fill([0, 0, 0, 255]).flat(),
      255,
      255,
      255,
      255,
      192,
      128,
      32,
      255,
      192,
      128,
      32,
      255,
      32,
      64,
      192,
      255
    ]);
    expect(detectPortraitColor(pixels)).toBe('rgb(192, 128, 32)');
  });

  it('groups similar shades without introducing colors absent from the portrait', () => {
    expect(
      detectPortraitColor(
        new Uint8ClampedArray([192, 128, 32, 255, 200, 136, 40, 255, 32, 64, 192, 255])
      )
    ).toBe('rgb(196, 132, 36)');
  });

  it('returns no accent for empty, transparent or near-black artwork', () => {
    expect(detectPortraitColor(new Uint8ClampedArray())).toBeNull();
    expect(detectPortraitColor(new Uint8ClampedArray([10, 10, 10, 255, 200, 0, 0, 0]))).toBeNull();
  });
});
