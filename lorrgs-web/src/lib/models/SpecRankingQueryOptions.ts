export type SpecRankingQueryOptions = {
  zoneId?: number;
  encounterId?: number;
  className?: string;
  specName?: string;
  difficultyId?: number;
  partition?: number;
  bracket?: number;
  serverRegion?: string;
  serverSlug?: string;
  filter?: string;
  hardModeLevel?: string;
  externalBuffs?: string;
  includeCombatantInfo?: boolean;
  includeOtherPlayers?: boolean;
};
