import type { RaidEdition } from './RaidEdition';
import type { RaidInstance } from './RaidInstance';

export type RaidCatalog = {
  editions: RaidEdition[];
  instances: Record<string, RaidInstance[]>;
};
