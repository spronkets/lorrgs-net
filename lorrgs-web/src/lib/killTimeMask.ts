import type { MaskInputOptions } from 'maska';

export const killTimeMask: MaskInputOptions = {
  mask: (value) => {
    const colon = value.indexOf(':');
    const minutes = colon >= 0 ? colon : value.replace(/\D/g, '').length - 2;
    return `${'#'.repeat(Math.max(1, minutes))}:S#`;
  },
  tokens: { S: { pattern: /[0-5]/ } }
};
