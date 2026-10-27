export function detectPortraitColor(pixels: Uint8ClampedArray): string | null {
  const colors = new Map<number, { weight: number; red: number; green: number; blue: number }>();
  for (let index = 0; index < pixels.length; index += 4) {
    const [red, green, blue, alpha] = pixels.subarray(index, index + 4);
    const brightest = Math.max(red, green, blue);
    const darkest = Math.min(red, green, blue);
    if (alpha < 128 || brightest < 64 || darkest > 225) continue;
    const weight = (alpha / 255) * (brightest / 255) * (1 + (brightest - darkest) / brightest);
    const key = ((red >> 5) << 6) | ((green >> 5) << 3) | (blue >> 5);
    const color = colors.get(key) ?? { weight: 0, red: 0, green: 0, blue: 0 };
    color.weight += weight;
    color.red += red * weight;
    color.green += green * weight;
    color.blue += blue * weight;
    colors.set(key, color);
  }
  const dominant = [...colors.values()].sort((a, b) => b.weight - a.weight)[0];
  return dominant
    ? `rgb(${Math.round(dominant.red / dominant.weight)}, ${Math.round(dominant.green / dominant.weight)}, ${Math.round(dominant.blue / dominant.weight)})`
    : null;
}
export function fitPortraitArtwork(
  pixels: Uint8ClampedArray,
  width: number,
  height: number
): string {
  let left = width,
    top = height,
    right = -1,
    bottom = -1;
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      if (!(pixels[(y * width + x) * 4 + 3] > 16)) continue;
      left = Math.min(left, x);
      right = Math.max(right, x);
      top = Math.min(top, y);
      bottom = Math.max(bottom, y);
    }
  }
  if (right < left || bottom < top) return '';
  const span = Math.max(right - left + 1, bottom - top + 1);
  return `width: ${(width / span) * 100}%; height: ${(height / span) * 100}%; left: ${50 - ((left + (right - left + 1) / 2) / span) * 100}%; top: ${50 - ((top + (bottom - top + 1) / 2) / span) * 100}%`;
}
