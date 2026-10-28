import { describe, expect, it } from 'vitest';
import { detectPortraitColor, fitPortraitArtwork, portraitArtworkLayout } from './portraitColor';

it('anchors lighting to the visible alpha-weighted portrait rather than the frame center', () => {
  const pixels = new Uint8ClampedArray(10 * 10 * 4);
  for (let x = 0; x < 10; x++) pixels[(8 * 10 + x) * 4 + 3] = 255;
  pixels[(7 * 10 + 1) * 4 + 3] = 255;
  const layout = portraitArtworkLayout(pixels, 10, 10);
  expect(layout.lightY).toBeGreaterThan(90);
  expect(layout.lightX).toBeLessThan(50);
  expect(layout.fit).toBe(fitPortraitArtwork(pixels, 10, 10));
  expect(portraitArtworkLayout(new Uint8ClampedArray(16), 2, 2)).toEqual({
    fit: '',
    lightX: 50,
    lightY: 50
  });
});

it('fits, horizontally centers and bottom-aligns visible artwork rather than transparent padding', () => {
  const pixels = new Uint8ClampedArray(128 * 64 * 4);
  for (let y = 2; y <= 62; y++) {
    for (let x = 10; x <= 97; x++) pixels[(y * 128 + x) * 4 + 3] = 255;
  }
  const style = fitPortraitArtwork(pixels, 128, 64);
  const values = Object.fromEntries(style.split('; ').map((part) => part.split(': ')));
  const scale = parseFloat(values.width) / 128;
  expect(parseFloat(values.left) + 10 * scale).toBeCloseTo(0);
  expect(parseFloat(values.left) + 98 * scale).toBeCloseTo(100);
  expect(parseFloat(values.top) + 63 * scale).toBeCloseTo(100);
  expect(parseFloat(values.top) + 2 * scale).toBeGreaterThanOrEqual(0);
  const [originX, originY] = values['transform-origin'].split(' ').map(parseFloat);
  expect(originX).toBeCloseTo((54 / 128) * 100);
  expect(originY).toBeCloseTo((63 / 64) * 100);
  const anchorY = parseFloat(values.top) + (originY / 100) * parseFloat(values.height);
  const bottomY = parseFloat(values.top) + 63 * scale;
  expect(anchorY + (bottomY - anchorY) * 1.2).toBeCloseTo(100);
  expect(fitPortraitArtwork(new Uint8ClampedArray(16), 2, 2)).toBe('');
});

it('keeps tall portraits fully fitted and horizontally centered', () => {
  const pixels = new Uint8ClampedArray(8 * 12 * 4);
  for (let y = 1; y < 11; y++) for (let x = 2; x < 6; x++) pixels[(y * 8 + x) * 4 + 3] = 255;
  const values = Object.fromEntries(
    fitPortraitArtwork(pixels, 8, 12)
      .split('; ')
      .map((part) => part.split(': '))
  );
  const scale = parseFloat(values.height) / 12;
  expect(parseFloat(values.top) + scale).toBeCloseTo(0);
  expect(parseFloat(values.top) + 11 * scale).toBeCloseTo(100);
  expect(parseFloat(values.left) + 4 * scale).toBeCloseTo(50);
});

it('excludes removed shadow padding while keeping every visible portrait pixel in frame', () => {
  const pixels = new Uint8ClampedArray(10 * 10 * 4);
  for (let index = 3; index < pixels.length; index += 4) pixels[index] = 180;
  for (let y = 3; y < 8; y++) for (let x = 2; x < 8; x++) pixels[(y * 10 + x) * 4 + 3] = 255;
  pixels[(3 * 10 + 2) * 4 + 3] = 205;
  const values = Object.fromEntries(
    portraitArtworkLayout(pixels, 10, 10)
      .fit.split('; ')
      .map((part) => part.split(': '))
  );
  const scale = parseFloat(values.width) / 10;
  expect(scale).toBeGreaterThan(10);
  expect(parseFloat(values.left) + 2 * scale).toBeCloseTo(0);
  expect(parseFloat(values.left) + 8 * scale).toBeCloseTo(100);
  expect(parseFloat(values.top) + 3 * scale).toBeGreaterThanOrEqual(0);
  expect(parseFloat(values.top) + 8 * scale).toBeCloseTo(100);
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
