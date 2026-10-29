import type { SpellPreset } from './SpellPreset';

export type ClassSpellPreset = {
  key: string;
  label: string;
  role: SpellPreset;
  spec?: string;
  icon: string;
};
