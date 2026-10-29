import type { WowClass } from './WowClass';
import type { WowRole } from './WowRole';

export type WowSpec = {
  id: number;
  name: string;
  nameSlug: string;
  fullName: string;
  fullNameSlug: string;
  classId: number;
  roleId: number;
  wowClass?: WowClass | null;
  role?: WowRole | null;
};
