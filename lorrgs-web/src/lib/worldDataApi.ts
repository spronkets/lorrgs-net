import { get, normalizeApiCollection } from './common/http/apiClient';

function worldDataEndpoint(path: string, edition: string): string {
  if (!edition.trim()) throw new Error('An edition is required for world-data requests.');
  return `/worlddata/${path}?${new URLSearchParams({ edition })}`;
}

export async function getSeasons(edition: string) {
  const payload = await get(worldDataEndpoint('seasons', edition));
  return normalizeApiCollection(payload, 'seasons');
}

export async function getSeason(slug: string, edition: string) {
  return get(worldDataEndpoint(`seasons/${slug}`, edition));
}

export async function getClasses(edition: string) {
  const payload = await get(worldDataEndpoint('classes', edition));
  return normalizeApiCollection(payload, 'classes');
}

export async function getSpecs(edition: string) {
  const payload = await get(worldDataEndpoint('specs', edition));
  return normalizeApiCollection(payload, 'specs');
}

export async function getRoles(edition: string) {
  const payload = await get(worldDataEndpoint('roles', edition));
  return normalizeApiCollection(payload, 'roles');
}

export async function getZones(edition: string) {
  const payload = await get(worldDataEndpoint('zones', edition));
  return normalizeApiCollection(payload, 'zones');
}

export async function getZone(slug: string, edition: string) {
  return get(worldDataEndpoint(`zones/${slug}`, edition));
}

export async function getZoneBosses(zoneSlug: string, edition: string) {
  const payload = await get(worldDataEndpoint(`zones/${zoneSlug}/bosses`, edition));
  return normalizeApiCollection(payload, 'bosses');
}

export async function getBosses(edition: string) {
  const payload = await get(worldDataEndpoint('bosses', edition));
  return normalizeApiCollection(payload, 'bosses');
}

export async function getBoss(slug: string, edition: string) {
  return get(worldDataEndpoint(`bosses/${slug}`, edition));
}

export async function getSpells(edition: string) {
  const payload = await get(worldDataEndpoint('spells', edition));
  return normalizeApiCollection(payload, 'spells');
}

export async function getSpell(id: string | number, edition: string) {
  return get(worldDataEndpoint(`spells/${id}`, edition));
}

export async function getTrinkets(edition: string) {
  const payload = await get(worldDataEndpoint('trinkets', edition));
  return normalizeApiCollection(payload, 'trinkets');
}

export async function getTrinket(id: string | number, edition: string) {
  return get(worldDataEndpoint(`trinkets/${id}`, edition));
}
