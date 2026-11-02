<script lang="ts">
  import { CalendarDays } from '@lucide/svelte';
  import { onMount, onDestroy, tick } from 'svelte';
  import { maska } from 'maska/svelte';
  import { killTimeMask } from '../lib/killTimeMask';

  import Alert from '../lib/components/Alert.svelte';
  import Spinner from '../lib/components/Spinner.svelte';
  import EncounterFilters from '../lib/components/EncounterFilters.svelte';
  import PageHeader from '../lib/components/PageHeader.svelte';
  import CompRankingsResults from '../lib/components/CompRankingsResults.svelte';
  import * as API from '../lib/api';
  import type { CompCountFilter as Filter } from '../lib/models/CompCountFilter';
  import type { CompRankingData } from '../lib/models/CompRankingData';
  import type { WorldDataSnapshot } from '../lib/models/WorldDataSnapshot';
  import {
    EDITION_OPTIONS,
    getApiEditionSlug,
    getAvailableSpecsForVersion,
    getRaidBossOptions,
    getRaidsForEdition
  } from '../lib/raidCatalog';
  import type { RaidCatalog } from '../lib/models/RaidCatalog';
  import { parseKillTime, toKillTimeInput } from '../lib/rankingUtils';
  import { getRoleIconUrl, getSpecIconUrl } from '../lib/wowAssets';

  export let worldData: WorldDataSnapshot = {};
  export let raidCatalog: RaidCatalog = { editions: [], instances: {} };
  export let selectedVersion = 'Retail';
  export let oneditionchange: ((_version: string) => void) | undefined = undefined;
  const versions = EDITION_OPTIONS;
  const operators = [
    { value: 'eq', label: '=' },
    { value: 'lt', label: '<' },
    { value: 'gt', label: '>' },
    { value: 'lte', label: '≤' },
    { value: 'gte', label: '≥' }
  ];
  const rankingRegions = ['', 'us', 'eu', 'kr', 'tw', 'cn'] as const;
  const difficultyOptionsFor = (version: string) =>
    version === 'Retail'
      ? [
          { value: 2, label: 'LFR' },
          { value: 3, label: 'Normal' },
          { value: 4, label: 'Heroic' },
          { value: 5, label: 'Mythic' }
        ]
      : version === 'Mists of Pandaria'
        ? [
            { value: 2, label: 'LFR' },
            { value: 3, label: 'Normal' },
            { value: 4, label: 'Heroic' }
          ]
        : [{ value: 3, label: 'Normal' }];
  const defaultDifficultyFor = (version: string) => (version === 'Retail' ? 5 : 3);
  let nextId = 0;
  let filters: Filter[] = [];
  let selectedRaid = '';
  let selectedBoss = '';
  let difficultyId = 5;
  let minTime = '';
  let maxTime = '';
  let region = '';
  let fromDate = '';
  let toDate = '';
  let sort: 'rank' | 'performance' | 'duration' | 'date' = 'rank';
  let page = 1;
  let appliedFilters = {
    roles: [] as string[],
    specs: [] as string[],
    minSeconds: 0,
    maxSeconds: 0,
    region: '',
    fromDate: '',
    toDate: '',
    sort: 'rank' as const
  };
  let detailRefresh = false;
  let rankings: CompRankingData | null = null;
  let loading = false;
  let error = '';
  let controller: AbortController | undefined;
  let requestId = 0;
  let lastSelection = '';
  $: raids = getRaidsForEdition(raidCatalog, selectedVersion);
  $: raid = raids.find((item) => item.slug === selectedRaid && item.isAvailable !== false);
  $: bosses = getRaidBossOptions(raid).map((boss) => ({
    ...boss,
    iconName: worldData.bosses?.find((entry) => entry.nameSlug === boss.slug)?.icon
  }));
  $: boss = bosses.find((item) => item.slug === selectedBoss);
  $: difficultyOptions = difficultyOptionsFor(selectedVersion);
  $: if (!difficultyOptions.some((option) => option.value === difficultyId)) {
    difficultyId = defaultDifficultyFor(selectedVersion);
  }
  $: versionSpecs = getAvailableSpecsForVersion(worldData.specs ?? [], selectedVersion);
  $: rolesById = new Map((worldData.roles ?? []).map((role) => [Number(role.id), role]));
  $: groupedVersionSpecs = buildGroupedSpecs(versionSpecs);
  $: unmatchedSpecFilters = filters
    .filter((filter) => filter.type === 'spec' && !versionSpecs.some((spec) => spec.fullNameSlug === filter.key))
    .filter((filter, index, list) => list.findIndex((item) => item.key === filter.key) === index);
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
    difficultyId = defaultDifficultyFor(version);
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

  function roleGroup(spec: { roleId: number }) {
    const role = rolesById.get(Number(spec.roleId));
    const code = String(role?.code || '').toLowerCase();
    const name = String(role?.name || '').toLowerCase();
    if (code === 'dps' || code === 'mdps' || code === 'rdps' || name.includes('dps')) return 'dps';
    if (code === 'heal' || name.includes('heal')) return 'healer';
    if (code === 'tank' || name.includes('tank')) return 'tank';
    return 'other';
  }

  function buildGroupedSpecs(specs: typeof versionSpecs) {
    const buckets: Record<'dps' | 'healer' | 'tank' | 'other', typeof specs> = {
      dps: [],
      healer: [],
      tank: [],
      other: []
    };
    for (const spec of specs) buckets[roleGroup(spec)].push(spec);
    const labels: Record<'dps' | 'healer' | 'tank' | 'other', string> = {
      dps: 'Damage',
      healer: 'Healers',
      tank: 'Tanks',
      other: 'Other'
    };
    return (['dps', 'healer', 'tank', 'other'] as const)
      .map((key) => ({
        key,
        label: labels[key],
        specs: buckets[key].sort((a, b) => a.fullName.localeCompare(b.fullName))
      }))
      .filter((group) => group.specs.length > 0);
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
    region = params.get('region') ?? '';
    fromDate = params.get('fromDate') ?? '';
    toDate = params.get('toDate') ?? '';
    sort =
      (params.get('sort') as 'rank' | 'performance' | 'duration' | 'date' | null) ?? 'rank';
    if (!['rank', 'performance', 'duration', 'date'].includes(sort)) sort = 'rank';
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
      'killtime_max',
      'region',
      'fromDate',
      'toDate',
      'sort'
    ])
      url.searchParams.delete(key);
    url.searchParams.set('edition', selectedVersion);
    url.searchParams.set('raid', selectedRaid);
    url.searchParams.set('boss', selectedBoss);
    url.searchParams.set('difficultyId', String(difficultyId));
    url.searchParams.set('compPage', String(page));
    if (minTime) url.searchParams.set('killtime_min', minTime);
    if (maxTime) url.searchParams.set('killtime_max', maxTime);
    if (region) url.searchParams.set('region', region);
    if (fromDate) url.searchParams.set('fromDate', fromDate);
    if (toDate) url.searchParams.set('toDate', toDate);
    if (sort !== 'rank') url.searchParams.set('sort', sort);
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
    if (!boss || !raid || !selectedVersion) {
      error = 'Select an edition, raid, and boss before searching.';
      return;
    }
    if (!boss.id && !raid.zoneId) {
      error =
        'Selected raid boss is missing encounter and zone mappings. Please choose a mapped raid.';
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
    if (fromDate && toDate && fromDate > toDate) {
      error = 'From date must not be after To date.';
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
        {
          encounterId: boss.id ?? 0,
          zoneId: raid.zoneId,
          difficultyId,
          page,
          minSeconds,
          maxSeconds,
          refresh
        },
        controller.signal
      );
      if (id === requestId) {
        appliedFilters = {
          roles: expressions('role'),
          specs: expressions('spec'),
          minSeconds,
          maxSeconds,
          region,
          fromDate,
          toDate,
          sort
        };
        detailRefresh = refresh;
        rankings = result;
      }
    } catch (err: unknown) {
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
  <PageHeader
    title="Comp Rankings"
    description="Compare raid-wide cooldown usage across execution-ranked kills that match your composition criteria."
  />
  <form
    onsubmit={(event) => {
      event.preventDefault();
      void search();
    }}
  >
    <section class="encounter-and-difficulty" aria-label="Encounter and difficulty selection">
      <EncounterFilters
        {worldData}
        {raidCatalog}
        bind:selectedVersion
        bind:selectedRaid
        bind:selectedBoss
        oneditionchange={editionChanged}
      />
      {#if difficultyOptions.length > 1}
        <label class="difficulty-control"
          >Difficulty
          <select bind:value={difficultyId} aria-label="Difficulty">
            {#each difficultyOptions as option (option.value)}
              <option value={option.value}>{option.label}</option>
            {/each}
          </select>
        </label>
      {/if}
    </section>
    <section class="analytics" aria-label="Ranking filters">
      <div class="analytics-filters">
        <fieldset class="filter-group region-filter">
          <legend>Region</legend>
          {#each rankingRegions as regionOption (regionOption)}
            <button
              class={['button', ['comp-rankings-page-button']]}
              data-tone={region === regionOption ? 'primary' : 'secondary'}
              data-outline={region !== regionOption}
              data-size="small"
              type="button"
              aria-label={regionOption ? `Region ${regionOption.toUpperCase()}` : 'All regions'}
              aria-pressed={region === regionOption}
              onclick={() => (region = regionOption)}
              >{regionOption ? regionOption.toUpperCase() : 'All'}</button
            >
          {/each}
        </fieldset>
        <fieldset class="filter-group duration-filter">
          <legend>Kill time</legend>
          <div class="form-field">
            <div class="input-frame">
              <input
                id="comprankingspage-field-1"
                class="input"
                aria-label="Minimum kill time"
                use:maska={killTimeMask}
                type="text"
                placeholder="m:ss"
                bind:value={minTime}
                onblur={() => (minTime = toKillTimeInput(minTime))}
              />
            </div>
          </div>
          <span aria-hidden="true">–</span>
          <div class="form-field">
            <div class="input-frame">
              <input
                id="comprankingspage-field-2"
                class="input"
                aria-label="Maximum kill time"
                use:maska={killTimeMask}
                type="text"
                placeholder="m:ss"
                bind:value={maxTime}
                onblur={() => (maxTime = toKillTimeInput(maxTime))}
              />
            </div>
          </div>
        </fieldset>
        <fieldset class="filter-group date-filter">
          <legend>Date range</legend>
          <div class="form-field">
            <div class="input-frame with-control-icon date-control">
              <input
                id="comprankingspage-field-3"
                class="input"
                aria-label="From date"
                type="date"
                bind:value={fromDate}
              />
              <CalendarDays class="control-icon" size={16} aria-hidden="true" />
            </div>
          </div>
          <span aria-hidden="true">–</span>
          <div class="form-field">
            <div class="input-frame with-control-icon date-control">
              <input
                id="comprankingspage-field-4"
                class="input"
                aria-label="To date"
                type="date"
                bind:value={toDate}
              />
              <CalendarDays class="control-icon" size={16} aria-hidden="true" />
            </div>
          </div>
        </fieldset>
        <label
          >Sort by
          <select bind:value={sort}>
            <option value="rank">Original rank</option>
            <option value="performance">Performance (highest)</option>
            <option value="duration">Kill time (shortest)</option>
            <option value="date">Date (newest)</option>
          </select>
        </label>
        <button
          class={['button', ['comp-rankings-page-button']]}
          data-tone="secondary"
          data-outline="true"
          data-size="small"
          type="button"
          onclick={() => {
            region = '';
            minTime = '';
            maxTime = '';
            fromDate = '';
            toDate = '';
            sort = 'rank';
          }}>Reset filters</button
        >
      </div>
    </section>
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
        {#each countFilters(type, key, filters) as filter (filter.id)}
          <div class="form-field">
            <div class="input-frame affixed compact">
              <span class="prefix"
                ><span class="filter-identity" title={label}>
                  {#if icon}<img src={icon} alt="" />{:else}<span>{shortLabel}</span>{/if}
                </span></span
              ><input
                id={`comp-${type}-${key}-${filter.id}`}
                class="input compact"
                aria-label={`${label} count`}
                type="number"
                min="0"
                max="100"
                step="1"
                placeholder="Any"
                value={filter.count ?? ''}
                oninput={(event) => updateCount(type, key, filter.id, event.currentTarget.value)}
              /><span class="suffix"
                ><select
                  class="comparison"
                  aria-label={`${label} comparison`}
                  value={filter.op}
                  disabled={filter.id === -1}
                  onchange={(event) =>
                    (filters = filters.map((item) =>
                      item.id === filter.id ? { ...item, op: event.currentTarget.value } : item
                    ))}
                >
                  {#each operators as op (op.value)}<option value={op.value}>{op.label}</option
                    >{/each}
                </select></span
              >
            </div>
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
          <h3 class="filter-heading">Specializations</h3>
          {#each groupedVersionSpecs as group (group.key)}
            <h4 class="spec-group-label">{group.label}</h4>
            <div class="composition-grid">
              {#each group.specs as spec (spec.fullNameSlug)}
                {@render countControl(
                  'spec',
                  spec.fullNameSlug,
                  spec.fullName,
                  specIcon(spec.classId, spec.nameSlug),
                  spec.name
                )}
              {/each}
            </div>
          {/each}
          {#if unmatchedSpecFilters.length > 0}
            <h4 class="spec-group-label">Other</h4>
            <div class="composition-grid">
              {#each unmatchedSpecFilters as filter (filter.key)}
                {@render countControl('spec', filter.key, `${filter.key} (upstream spec)`, '')}
              {/each}
            </div>
          {/if}
        </section>
      </div>
      <div class="actions clear-actions">
        <button
          class={['button', ['comp-rankings-page-button']]}
          data-tone="secondary"
          data-outline="true"
          data-size="small"
          type="button"
          onclick={() => {
            filters = [];
          }}>Clear composition filters</button
        >
      </div>
    </section>
    <div class="actions">
      <button
        class={['button', ['comp-rankings-page-button', `primary`]]}
        data-tone="primary"
        data-outline="false"
        data-size="small"
        type="submit"
        disabled={!selectedBoss}>Search compositions</button
      >
      {#if loading}<button
          class={['button', ['comp-rankings-page-button']]}
          data-tone="secondary"
          data-outline="true"
          data-size="small"
          type="button"
          onclick={invalidate}>Cancel</button
        >{/if}
    </div>
  </form>
  {#if loading}<Spinner loadingText="Loading fight summaries…" />
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
  {:else}<Alert>Select a boss and search to view composition rankings.</Alert>{/if}
</section>

<style lang="scss">
  @use '../lib/styles/selection-tokens' as selectionTokens;

  .comp-page {
    @include selectionTokens.apply-selection-tokens;

    width: 100%;
    max-width: 1600px;
    margin: 0 auto;
  }
  .encounter-and-difficulty {
    display: grid;
    gap: var(--space-5, 0.8rem);
  }
  .difficulty-control {
    @include selectionTokens.selection-label;

    min-width: 0;
    display: flex;
    flex-direction: column;
    gap: 0.3rem;
  }
  .difficulty-control select {
    @include selectionTokens.selection-dropdown;

    width: 100%;
  }
  form {
    display: grid;
    gap: 1rem;
    margin-bottom: 1rem;
  }
  .analytics {
    padding: 0.35rem 0;
    border: 0;
    background: transparent;
    color: #2a4b69;
  }
  .analytics-filters {
    display: flex;
    flex-wrap: wrap;
    gap: 0.5rem;
    align-items: end;
  }
  .analytics-filters label {
    @include selectionTokens.selection-label;

    display: flex;
    flex-direction: column;
    gap: 0.3rem;
  }
  .analytics-filters select {
    @include selectionTokens.selection-dropdown;
  }
  .filter-group {
    display: flex;
    gap: 3px;
    align-items: center;
    border: 0;
    margin: 0;
    padding: 0;
    min-width: 0;
  }
  .filter-group legend {
    @include selectionTokens.selection-label;

    margin-bottom: 3px;
  }
  .duration-filter > :global(.form-field) {
    width: 105px;
  }
  .with-control-icon {
    position: relative;
  }
  .with-control-icon input {
    padding-right: 2rem;
  }
  .with-control-icon :global(.control-icon) {
    position: absolute;
    right: 0.65rem;
    top: 50%;
    transform: translateY(-50%);
    pointer-events: none;
    color: var(--select-text);
  }
  .date-control input {
    padding-right: 0.62rem;
  }
  .date-control input::-webkit-calendar-picker-indicator {
    position: absolute;
    right: 0.65rem;
    top: 50%;
    transform: translateY(-50%);
    opacity: 0;
    width: 16px;
    height: 16px;
    padding: 0;
    margin: 0;
    cursor: pointer;
  }
  @supports not selector(input::-webkit-calendar-picker-indicator) {
    .date-control :global(.control-icon) {
      display: none;
    }
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
  .spec-group-label {
    margin: 0.45rem 0 0.35rem;
    font-size: 0.82rem;
    font-weight: 700;
    letter-spacing: 0.06em;
    text-transform: uppercase;
    color: var(--section-heading);
  }
  .composition-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, 120px);
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
    align-items: center;
    gap: 0.25rem;
  }
  .composition-control.active {
    outline: 1px solid #c64a85;
    border-radius: 0.58rem;
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
  select.comparison {
    min-height: 44px;
    align-self: stretch;
    padding: 0.2rem;
    box-sizing: border-box;
    font-size: 0.8rem;
    width: 36px;
    flex-shrink: 0;
    padding-right: 12px;
    background-position: right 3px center;
    background-size: 8px;
    border: 0;
    border-radius: 0 0.58rem 0.58rem 0;
    background-color: transparent;
  }
  .clear-actions {
    margin-top: 1rem;
  }
  select.comparison:disabled {
    opacity: 0.5;
  }
  select {
    @include selectionTokens.selection-dropdown;
  }

  :global(.theme-dark) :global(button.comp-rankings-page-button) {
    background: #243447;
    color: #e6edf5;
  }
  @media (max-width: 600px) {
    .encounter-heading {
      align-items: start;
    }
    .difficulty-control {
      width: 100%;
    }
    .analytics .analytics-filters {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      gap: 8px;
    }
    .analytics .region-filter,
    .analytics .duration-filter {
      grid-column: 1 / -1;
    }
    .analytics .filter-group {
      gap: 6px;
    }
    .analytics .filter-group legend {
      margin-bottom: 5px;
    }
    .analytics .filter-group :global(button.comp-rankings-page-button) {
      flex: 1;
      min-width: 34px;
      min-height: 44px;
    }
    .analytics .duration-filter > :global(.form-field) {
      flex: 1;
      width: 0;
      min-width: 0;
    }
    .analytics .date-filter {
      flex-wrap: wrap;
    }
    .analytics .date-filter > :global(.form-field) {
      min-width: 0;
      flex: 1;
    }
    select {
      width: 100%;
      box-sizing: border-box;
    }
    .composition-grid {
      grid-template-columns: repeat(auto-fill, 120px);
    }
  }
</style>
