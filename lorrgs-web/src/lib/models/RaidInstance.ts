import type { RaidBossOption } from './RaidBossOption';

export type RaidInstance = {
  raidSize?: string | null;
  zoneId: number;
  slug: string;
  name: string;
  edition: string;
  phase: number;
  bosses?: RaidBossOption[];
  public: boolean;
};
