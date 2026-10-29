<script lang="ts">
  import { onMount, onDestroy, tick } from 'svelte';
  import { maska } from 'maska/svelte';
  import { killTimeMask } from '../killTimeMask';
  import Alert from '../components/Alert.svelte';
  import EncounterFilters from '../components/EncounterFilters.svelte';
  import RankingPageHeader from '../components/RankingPageHeader.svelte';
  import CompRankingsResults from '../components/CompRankingsResults.svelte';
  import BossIcon from '../components/BossIcon.svelte';
  import * as API from '../api';
  import type { CompRankingData, WorldDataSnapshot } from '../api';
  import {
    getApiEditionSlug,
    getAvailableSpecsForVersion,
    getRaidBossOptions,
    getRaidsForEdition,
    type RaidCatalog
  } from '../raidCatalog';
  import { parseKillTime, toKillTimeInput } from '../rankingUtils';
  import { getRoleIconUrl, getSpecIconUrl } from '../wowAssets';

  export let worldData: WorldDataSnapshot = {};
  export let raidCatalog: RaidCatalog = { editions: [], instances: {} };
  export let selectedVersion = 'Retail';
  export let oneditionchange: ((_version: string) => void) | undefined = undefined;
  const versions = ['Anniversary', 'Mists of Pandaria', 'Era', 'Retail'];
  const operators = [
    { value: 'eq', label: '=' },
    { value: 'lt', label: '<' },
    { value: 'gt', label: '>' },
    { value: 'lte', label: '≤' },
    { value: 'gte', label: '≥' }
  ];
  type Filter = { id: number; type: 'role' | 'spec'; key: string; op: string; count: number };
  let nextId = 0;
  let filters: Filter[] = [];
  let selectedRaid = '';
  let selectedBoss = '';
  let difficultyId = 5;
  let minTime = '';
  let maxTime = '';
  let page = 1;
  let appliedFilters = {
    roles: [] as string[],
    specs: [] as string[],
    minSeconds: 0,
    maxSeconds: 0
  };
  let detailRefresh = false;
  let rankings: CompRankingData | null = null;
  let loading = false;
  let error = '';
  let controller: AbortController | undefined;
  let requestId = 0;
  let lastSelection = '';
  $: raids = getRaidsForEdition(raidCatalog, selectedVersion);
  $: raid = raids.find((item) => item.slug === selectedRaid);
  $: bosses = getRaidBossOptions(raid).map((boss) => ({
    ...boss,
    iconName: worldData.bosses?.find((entry) => entry.nameSlug === boss.slug)?.icon
  }));
  $: boss = bosses.find((item) => item.slug === selectedBoss);
  $: versionSpecs = getAvailableSpecsForVersion(worldData.specs ?? [], selectedVersion);
  $: selection = `${selectedVersion}:${selectedRaid}:${selectedBoss}:${difficultyId}`;
  $: invalidateSelection(selection);

  function invalidateSelection(next: string) {
    if (next !== lastSelection) {
      lastSelection = next;
      invalidate();
    }
  }

  function invalidate() {
    controller?.abort();
    requestId++;
    rankings = null;
    loading = false;
    error = '';
  }

  function editionChanged(version: string) {
    invalidate();
    selectedVersion = version;
    selectedRaid = '';
    selectedBoss = '';
    difficultyId = version === 'Retail' ? 5 : 3;
    filters = [];
    oneditionchange?.(version);
  }

  function countFilters(type: Filter['type'], key: string, currentFilters: Filter[]) {
    const active = currentFilters.filter((filter) => filter.type === type && filter.key === key);
    return active.length ? active : [{ id: -1, type, key, op: 'eq', count: undefined }];
  }

  function updateCount(type: Filter['type'], key: string, id: number, value: string) {
    if (!value.trim()) {
      filters = filters.filter((filter) => filter.id !== id);
    } else if (id === -1) {
      filters = [...filters, { id: nextId++, type, key, op: 'eq', count: Number(value) }];
    } else {
      filters = filters.map((filter) =>
        filter.id === id ? { ...filter, count: Number(value) } : filter
      );
    }
  }

  function specIcon(classId: number, nameSlug: string) {
    const wowClass = worldData.classes?.find((item) => item.id === classId);
    return wowClass ? getSpecIconUrl(wowClass.nameSlug, nameSlug) : '';
  }

  function hydrate() {
    const params = new URLSearchParams(window.location.search);
    const version = params.get('edition');
    if (version && versions.includes(version)) {
      selectedVersion = version;
      oneditionchange?.(version);
    }
    selectedRaid = params.get('raid') ?? '';
    selectedBoss = params.get('boss') ?? '';
    difficultyId = Number(params.get('difficultyId') ?? (selectedVersion === 'Retail' ? 5 : 3));
    page = Number(params.get('compPage') ?? 1);
    minTime = toKillTimeInput(params.get('killtime_min') ?? '');
    maxTime = toKillTimeInput(params.get('killtime_max') ?? '');
    filters = (['role', 'spec'] as const).flatMap((type) =>
      params
        .getAll(type)
        .filter((expression) => expression.includes('.'))
        .map((expression) => {
          const parts = expression.split('.');
          return {
            id: nextId++,
            type,
            key: parts[0],
            op: parts.length === 2 ? 'eq' : parts[1],
            count: Number(parts.at(-1))
          };
        })
    );
    return Boolean(selectedBoss && params.has('compPage'));
  }

  function syncUrl() {
    const url = new URL(window.location.href);
    for (const key of [
      'role',
      'spec',
      'edition',
      'raid',
      'boss',
      'difficultyId',
      'compPage',
      'limit',
      'killtime_min',
      'killtime_max'
    ])
      url.searchParams.delete(key);
    url.searchParams.set('edition', selectedVersion);
    url.searchParams.set('raid', selectedRaid);
    url.searchParams.set('boss', selectedBoss);
    url.searchParams.set('difficultyId', String(difficultyId));
    url.searchParams.set('compPage', String(page));
    if (minTime) url.searchParams.set('killtime_min', minTime);
    if (maxTime) url.searchParams.set('killtime_max', maxTime);
    for (const filter of filters)
      url.searchParams.append(filter.type, `${filter.key}.${filter.op}.${filter.count}`);
    window.history.pushState({}, '', `${url.pathname}${url.search}${url.hash}`);
  }

  async function search(refresh = false, nextPage = 1, updateUrl = true) {
    controller?.abort();
    const id = ++requestId;
    rankings = null;
    error = '';
    loading = false;
    const minSeconds = minTime ? (parseKillTime(minTime) ?? NaN) / 1000 : 0;
    const maxSeconds = maxTime ? (parseKillTime(maxTime) ?? NaN) / 1000 : 0;
    if (!boss?.id || !raid || !selectedVersion) {
      error = 'Select an edition, raid, and boss before searching.';
      return;
    }
    if (
      !Number.isFinite(minSeconds) ||
      !Number.isFinite(maxSeconds) ||
      (maxSeconds > 0 && maxSeconds < minSeconds)
    ) {
      error = 'Enter kill times as m:ss, with maximum at least minimum.';
      return;
    }
    if (
      filters.some(
        (filter) =>
          !filter.key ||
          !operators.some((op) => op.value === filter.op) ||
          !Number.isInteger(filter.count) ||
          filter.count < 0 ||
          filter.count > 100
      )
    ) {
      error = 'Choose a role or spec, a comparison, and a count from 0 to 100 for every filter.';
      return;
    }
    page = nextPage;
    if (updateUrl) syncUrl();
    controller = new AbortController();
    loading = true;
    try {
      const expressions = (type: 'role' | 'spec') =>
        filters
          .filter((filter) => filter.type === type)
          .map((filter) => `${filter.key}.${filter.op}.${filter.count}`);
      const result = await API.getCompRankings(
        selectedBoss,
        expressions('role'),
        expressions('spec'),
        getApiEditionSlug(selectedVersion),
        { encounterId: boss.id, difficultyId, page, minSeconds, maxSeconds, refresh },
        controller.signal
      );
      if (id === requestId) {
        appliedFilters = {
          roles: expressions('role'),
          specs: expressions('spec'),
          minSeconds,
          maxSeconds
        };
        detailRefresh = refresh;
        rankings = result;
      }
    } catch (err) {
      if (id === requestId && !controller.signal.aborted)
        error = err instanceof Error ? err.message : 'Unable to load composition rankings.';
    } finally {
      if (id === requestId) loading = false;
    }
  }

  async function restore() {
    invalidate();
    const shouldSearch = hydrate();
    await tick();
    if (shouldSearch) await search(false, page, false);
  }
  onMount(() => {
    void restore();
    window.addEventListener('popstate', restore);
    return () => window.removeEventListener('popstate', restore);
  });
  onDestroy(() => controller?.abort());
</script>

<section class="comp-page">
  <RankingPageHeader
    title="Comp Rankings"
    description="Compare raid-wide cooldown usage across execution-ranked kills that match your composition criteria."
  />
  {#if boss}
    <div class="encounter-heading">
      <BossIcon slug={boss.slug} iconName={boss.iconName} alt={boss.name} size={40} />
      <div>
        <strong>{boss.name}</strong>
        <div>{selectedVersion} / {raid?.name}</div>
      </div>
    </div>
  {/if}
  <form on:submit|preventDefault={() => search()}>
    <EncounterFilters
      {worldData}
      {raidCatalog}
      bind:selectedVersion
      bind:selectedRaid
      bind:selectedBoss
      oneditionchange={editionChanged}
    />
    <div class="search-fields">
      <label
        >Difficulty
        <select bind:value={difficultyId}>
          {#if selectedVersion === 'Retail' || selectedVersion === 'Mists of Pandaria'}<option
              value={2}>LFR</option
            >{/if}
          <option value={3}>Normal</option>
          {#if selectedVersion === 'Retail' || selectedVersion === 'Mists of Pandaria'}<option
              value={4}>Heroic</option
            >{/if}
          {#if selectedVersion === 'Retail'}<option value={5}>Mythic</option>{/if}
        </select>
      </label>
      <label
        >Minimum kill time<input
          aria-label="Minimum kill time"
          use:maska={killTimeMask}
          type="text"
          placeholder="m:ss"
          bind:value={minTime}
          on:blur={() => (minTime = toKillTimeInput(minTime))}
        /></label
      >
      <label
        >Maximum kill time<input
          aria-label="Maximum kill time"
          use:maska={killTimeMask}
          type="text"
          placeholder="m:ss"
          bind:value={maxTime}
          on:blur={() => (maxTime = toKillTimeInput(maxTime))}
        /></label
      >
    </div>
    {#snippet countControl(
      type: Filter['type'],
      key: string,
      label: string,
      icon: string,
      shortLabel = label
    )}
      <div
        class="composition-control"
        class:active={filters.some((filter) => filter.type === type && filter.key === key)}
      >
        <div class="filter-identity" title={label}>
          {#if icon}<img src={icon} alt="" />{/if}
          {#if !icon}<span>{shortLabel}</span>{/if}
        </div>
        {#each countFilters(type, key, filters) as filter (filter.id)}
          <div class="count-inputs">
            <select
              aria-label={`${label} comparison`}
              value={filter.op}
              disabled={filter.id === -1}
              on:change={(event) =>
                (filters = filters.map((item) =>
                  item.id === filter.id ? { ...item, op: event.currentTarget.value } : item
                ))}
            >
              {#each operators as op (op.value)}<option value={op.value}>{op.label}</option>{/each}
            </select>
            <input
              aria-label={`${label} count`}
              type="number"
              min="0"
              max="100"
              step="1"
              placeholder="Any"
              value={filter.count ?? ''}
              on:input={(event) => updateCount(type, key, filter.id, event.currentTarget.value)}
            />
          </div>
        {/each}
      </div>
    {/snippet}
    <section class="composition-search" aria-label="Composition filters">
      <div class="composition-columns">
        <section aria-label="Role filters">
          <h3 class="filter-heading">Roles</h3>
          <div class="composition-grid">
            {#each worldData.roles ?? [] as role (role.code)}
              {@render countControl('role', role.code, role.name, getRoleIconUrl(role.code))}
            {/each}
          </div>
        </section>
        <section class="specialization-filters" aria-label="Specialization filters">
          <h3 class="filter-heading">Specs</h3>
          <div class="composition-grid">
            {#each versionSpecs as spec (spec.fullNameSlug)}
              {@render countControl(
                'spec',
                spec.fullNameSlug,
                spec.fullName,
                specIcon(spec.classId, spec.nameSlug),
                spec.name
              )}
            {/each}
            {#each filters
              .filter((filter) => filter.type === 'spec' && !versionSpecs.some((spec) => spec.fullNameSlug === filter.key))
              .filter((filter, index, list) => list.findIndex((item) => item.key === filter.key) === index) as filter (filter.key)}
              {@render countControl('spec', filter.key, `${filter.key} (upstream spec)`, '')}
            {/each}
          </div>
        </section>
      </div>
      <div class="actions clear-actions">
        <button
          type="button"
          on:click={() => {
            filters = [];
            minTime = '';
            maxTime = '';
          }}>Clear filters</button
        >
      </div>
    </section>
    <div class="actions">
      <button class="primary" type="submit" disabled={!selectedBoss}>Search compositions</button>
      <button type="button" disabled={!selectedBoss || loading} on:click={() => search(true, page)}
        >Refresh from Warcraft Logs</button
      >
      {#if loading}<button type="button" on:click={invalidate}>Cancel</button>{/if}
    </div>
  </form>
  {#if loading}<p role="status">Loading fight summaries…</p>
  {:else if error}<Alert variant="error">{error}</Alert>
  {:else if rankings}
    {#if !rankings.reports.length}<Alert>No kill summaries on this page.</Alert>{/if}
    {#key rankings}
      <CompRankingsResults
        {rankings}
        {worldData}
        edition={getApiEditionSlug(selectedVersion)}
        bossIconName={boss?.iconName}
        filters={appliedFilters}
        refresh={detailRefresh}
      />
    {/key}
    <div class="actions pagination">
      <button disabled={page <= 1} on:click={() => search(false, page - 1)}>Previous page</button>
      <span>Page {page}</span>
      <button disabled={!rankings.hasMorePages} on:click={() => search(false, page + 1)}
        >Next page</button
      >
    </div>
  {:else}<Alert>Select a boss and search to view composition rankings.</Alert>{/if}
</section>

<style lang="scss">
  @use '../styles/selection-tokens' as selectionTokens;

  .comp-page {
    @include selectionTokens.apply-selection-tokens;

    width: 100%;
    max-width: 1600px;
    margin: 0 auto;
  }
  .encounter-heading {
    display: flex;
    align-items: center;
    gap: 0.7rem;
    margin-bottom: 1rem;
  }
  .encounter-heading strong {
    font-size: 1.15rem;
  }
  .encounter-heading div div {
    font-size: 0.85rem;
    opacity: 0.8;
    margin-top: 0.25rem;
  }
  form {
    display: grid;
    gap: 1rem;
    margin-bottom: 1rem;
  }
  .search-fields {
    display: grid;
    grid-template-columns: repeat(3, minmax(0, 1fr));
    align-items: end;
    gap: var(--space-5);
  }
  .search-fields > label {
    min-width: 0;
  }
  .search-fields input,
  .search-fields select {
    width: 100%;
  }
  .actions {
    display: flex;
    flex-wrap: wrap;
    align-items: end;
    gap: 0.65rem;
  }
  label {
    @include selectionTokens.selection-label;

    display: grid;
    gap: var(--space-2);
  }
  .composition-search {
    min-width: 0;
  }
  .filter-heading {
    @include selectionTokens.selection-label;

    margin: 1rem 0 0.6rem;
  }
  .composition-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, 145px);
    gap: 0.4rem;
  }
  .composition-columns {
    display: grid;
    grid-template-columns: minmax(0, 1fr);
    gap: 0.5rem;
  }
  .specialization-filters {
    min-width: 0;
    margin-top: 0.75rem;
    padding-top: 0.5rem;
  }
  .composition-control {
    min-width: 0;
    display: grid;
    grid-template-columns: 28px minmax(0, 1fr);
    align-items: center;
    gap: 0.25rem;
    padding: 0.2rem;
    border: 1px solid color-mix(in srgb, currentColor 20%, transparent);
    border-radius: 0.4rem;
    background: color-mix(in srgb, currentColor 3%, transparent);
  }
  .composition-control.active {
    border-color: #c64a85;
  }
  .filter-identity {
    display: flex;
    align-items: center;
    gap: 0.25rem;
    min-height: 28px;
    margin-bottom: 0;
    font-size: 0.85rem;
    font-weight: 600;
  }
  .filter-identity img {
    width: 28px;
    height: 28px;
    border-radius: 3px;
  }
  .count-inputs {
    display: flex;
    gap: 0.2rem;
    min-width: 0;
  }
  .count-inputs + .count-inputs {
    grid-column: 2;
  }
  .count-inputs input,
  .count-inputs select {
    min-height: 32px;
    padding: 0.2rem;
    box-sizing: border-box;
    font-size: 0.8rem;
  }
  .count-inputs select {
    width: 42px;
    flex-shrink: 0;
    padding-right: 12px;
    background-position: right 3px center;
    background-size: 8px;
  }
  .count-inputs input[type='number'] {
    width: 0;
    min-width: 0;
    flex: 1;
  }
  .clear-actions {
    margin-top: 1rem;
  }
  .count-inputs select:disabled {
    opacity: 0.5;
  }
  input {
    @include selectionTokens.selection-control;
  }
  select {
    @include selectionTokens.selection-dropdown;
  }
  input::placeholder {
    color: var(--select-text);
    opacity: 0.6;
  }
  button {
    font: inherit;
    color: inherit;
    background: var(--color-surface, #f3f9ff);
    border: 1px solid #687e94;
    border-radius: 0.3rem;
    padding: 0.45rem 0.65rem;
    min-height: 44px;
  }
  input[type='number'] {
    width: 80px;
  }
  button {
    cursor: pointer;
  }
  button:hover:not(:disabled),
  .primary {
    border-color: #c64a85;
  }
  button:disabled {
    opacity: 0.5;
    cursor: default;
  }
  :global(.theme-dark) button {
    background: #243447;
    color: #e6edf5;
  }
  .pagination {
    justify-content: center;
    align-items: center;
    margin: 1rem 0;
  }
  @media (max-width: 600px) {
    .search-fields {
      grid-template-columns: minmax(0, 1fr);
    }
    input,
    select {
      width: 100%;
      box-sizing: border-box;
    }
    .composition-grid {
      grid-template-columns: repeat(auto-fill, minmax(130px, 1fr));
    }
    .count-inputs input,
    .count-inputs select {
      min-height: 44px;
    }
  }
</style>
