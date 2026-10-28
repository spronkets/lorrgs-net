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
  return portraitArtworkLayout(pixels, width, height).fit;
}

export function portraitArtworkLayout(
  pixels: Uint8ClampedArray,
  width: number,
  height: number
): { fit: string; lightX: number; lightY: number } {
  let left = width,
    top = height,
    right = -1,
    bottom = -1;
  let weight = 0,
    centerX = 0,
    centerY = 0;
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const sourceAlpha = pixels[(y * width + x) * 4 + 3] / 255;
      // Match the portrait filter's shadow-removal curve: alpha * 5 - 4.
      const alpha = Math.max(0, sourceAlpha * 5 - 4);
      if (!alpha) continue;
      left = Math.min(left, x);
      right = Math.max(right, x);
      top = Math.min(top, y);
      bottom = Math.max(bottom, y);
      weight += alpha;
      centerX += (x + 0.5) * alpha;
      centerY += (y + 0.5) * alpha;
    }
  }
  if (!weight) return { fit: '', lightX: 50, lightY: 50 };
  const span = Math.max(right - left + 1, bottom - top + 1);
  const offsetX = 50 - ((left + (right - left + 1) / 2) / span) * 100;
  const offsetY = 100 - ((bottom + 1) / span) * 100;
  return {
    fit: `width: ${(width / span) * 100}%; height: ${(height / span) * 100}%; left: ${offsetX}%; top: ${offsetY}%; transform-origin: ${((left + (right - left + 1) / 2) / width) * 100}% ${((bottom + 1) / height) * 100}%`,
    lightX: offsetX + (centerX / weight / span) * 100,
    lightY: offsetY + (centerY / weight / span) * 100
  };
}
