import type { WowRole } from './WowRole';
import type { WowClass } from './WowClass';
import type { WowSpec } from './WowSpec';
import type { WorldDataBoss } from './WorldDataBoss';
import type { WorldDataZone } from './WorldDataZone';
import type { WowSpell } from './WowSpell';
import type { WowTrinket } from './WowTrinket';
import type { Season } from './Season';

export type WorldDataSnapshot = {
  roles?: WowRole[];
  classes?: WowClass[];
  specs?: WowSpec[];
  bosses?: WorldDataBoss[];
  zones?: WorldDataZone[];
  spells?: WowSpell[];
  trinkets?: WowTrinket[];
  seasons?: Season[];
};
