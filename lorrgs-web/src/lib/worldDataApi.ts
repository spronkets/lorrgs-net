import { get, normalizeApiCollection } from './common/http/apiClient';
import type { WorldDataSnapshot } from './models/WorldDataSnapshot';
import type { Season } from './models/Season';
import type { WorldDataZone } from './models/WorldDataZone';
import type { WorldDataBoss } from './models/WorldDataBoss';
import type { WowSpell } from './models/WowSpell';
import type { WowTrinket } from './models/WowTrinket';

function worldDataEndpoint(path: string, edition: string): string {
  if (!edition.trim()) throw new Error('An edition is required for world-data requests.');
  return `/worlddata/${path}?${new URLSearchParams({ edition })}`;
}

export async function getSeasons(
  edition: string
): Promise<NonNullable<WorldDataSnapshot['seasons']>> {
  const payload = await get(worldDataEndpoint('seasons', edition));
  return normalizeApiCollection(payload, 'seasons');
}

export async function getSeason(slug: string, edition: string) {
  return get<Season>(worldDataEndpoint(`seasons/${slug}`, edition));
}

export async function getClasses(
  edition: string
): Promise<NonNullable<WorldDataSnapshot['classes']>> {
  const payload = await get(worldDataEndpoint('classes', edition));
  return normalizeApiCollection(payload, 'classes');
}

export async function getSpecs(edition: string): Promise<NonNullable<WorldDataSnapshot['specs']>> {
  const payload = await get(worldDataEndpoint('specs', edition));
  return normalizeApiCollection(payload, 'specs');
}

export async function getRoles(edition: string): Promise<NonNullable<WorldDataSnapshot['roles']>> {
  const payload = await get(worldDataEndpoint('roles', edition));
  return normalizeApiCollection(payload, 'roles');
}

export async function getZones(edition: string): Promise<NonNullable<WorldDataSnapshot['zones']>> {
  const payload = await get(worldDataEndpoint('zones', edition));
  return normalizeApiCollection(payload, 'zones');
}

export async function getZone(slug: string, edition: string) {
  return get<WorldDataZone>(worldDataEndpoint(`zones/${slug}`, edition));
}

export async function getZoneBosses(zoneSlug: string, edition: string): Promise<WorldDataBoss[]> {
  const payload = await get(worldDataEndpoint(`zones/${zoneSlug}/bosses`, edition));
  return normalizeApiCollection(payload, 'bosses');
}

export async function getBosses(
  edition: string
): Promise<NonNullable<WorldDataSnapshot['bosses']>> {
  const payload = await get(worldDataEndpoint('bosses', edition));
  return normalizeApiCollection(payload, 'bosses');
}

export async function getBoss(slug: string, edition: string) {
  return get<WorldDataBoss>(worldDataEndpoint(`bosses/${slug}`, edition));
}

export async function getSpells(
  edition: string
): Promise<NonNullable<WorldDataSnapshot['spells']>> {
  const payload = await get(worldDataEndpoint('spells', edition));
  return normalizeApiCollection(payload, 'spells');
}

export async function getSpell(id: string | number, edition: string) {
  return get<WowSpell>(worldDataEndpoint(`spells/${id}`, edition));
}

export async function getTrinkets(
  edition: string
): Promise<NonNullable<WorldDataSnapshot['trinkets']>> {
  const payload = await get(worldDataEndpoint('trinkets', edition));
  return normalizeApiCollection(payload, 'trinkets');
}

export async function getTrinket(id: string | number, edition: string) {
  return get<WowTrinket>(worldDataEndpoint(`trinkets/${id}`, edition));
}
