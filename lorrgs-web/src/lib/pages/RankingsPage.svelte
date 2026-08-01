<script lang="ts">
  import { onMount } from 'svelte'
  import * as API from '../api'
  import * as Cache from '../cache'
  import { getBlizzardClassColor } from '../classColors'
  import { getVersionIconUrl } from '../selectionIcons'
  import EditionSelector from '../components/EditionSelector.svelte'
  import RaidBossSelectors from '../components/RaidBossSelectors.svelte'
  import { getClassIconUrl, getSpecIconUrl } from '../wowAssets'
  import {
    getAvailableSpecsForVersion,
    getRaidBossOptions,
    getRaidsForVersion,
    groupRaidsByPhase
  } from '../raidCatalog'
  import RankingsList from '../components/RankingsList.svelte'

  export let worldData = {}
  export let raidCatalog = {
    editions: [],
    instances: {}
  }
  export let initialSpec = ''
  export let initialBoss = ''

  let selectedVersion = ''
  let selectedRaid = ''
  let selectedBoss = ''
  let selectedSpec = initialSpec
  let selectedDifficulty = 'Mythic'
  let selectedMetric = 'dps'
  let rankings = null
  let loading = false
  let error = ''
  let hasHydratedQuery = false

  const difficulties = ['Normal', 'Heroic', 'Mythic']
  const metrics = ['dps', 'hps', 'wdps']
  const versionOrder = ['Anniversary', 'Mists of Pandaria', 'Era', 'Retail']
  const versions = versionOrder
  const PAGE_QUERY_VALUE = 'rankings'

  $: raids = getRaidsForVersion(raidCatalog, selectedVersion)
  $: raidPhaseGroups = groupRaidsByPhase(raids)
  $: if (!selectedVersion && versions.length) {
    selectedVersion =
      versions.find((version) => getRaidsForVersion(raidCatalog, version).length > 0) || versions[0]
  }
  $: selectedRaidOption = raids.find((raid) => raid.slug === selectedRaid)
  $: bosses = getRaidBossOptions(selectedRaidOption)

  $: if (selectedRaid && !raids.some((raid) => raid.slug === selectedRaid)) {
    selectedRaid = ''
  }

  $: if (selectedBoss && !bosses.some((boss) => boss.slug === selectedBoss)) {
    selectedBoss = ''
  }

  $: availableSpecs = getAvailableSpecsForVersion(worldData.specs || [], selectedVersion)
  $: classesById = new Map((worldData.classes || []).map((cls) => [Number(cls.id), cls]))
  $: selectedSpecMeta = availableSpecs.find((spec) => spec.fullNameSlug === selectedSpec)
  $: selectedClassMeta = selectedSpecMeta ? getSpecClass(selectedSpecMeta) : null

  $: if (selectedSpec && !availableSpecs.some((spec) => spec.fullNameSlug === selectedSpec)) {
    selectedSpec = ''
  }

  function onVersionChange() {
    selectedRaid = ''
    selectedBoss = ''
  }

  function selectVersion(version) {
    if (selectedVersion === version) return
    selectedVersion = version
    onVersionChange()
  }

  function getSpecClass(spec) {
    return classesById.get(Number(spec.classId)) || null
  }

  function getSpecClassSlug(spec) {
    const matchedClass = getSpecClass(spec)
    return matchedClass?.nameSlug || 'other'
  }

  function getSpecClassColor(spec) {
    const matchedClass = getSpecClass(spec)
    return getBlizzardClassColor(matchedClass?.nameSlug, matchedClass?.color || '#7BA4BF')
  }

  function selectSpecFromGrid(specSlug) {
    selectedSpec = specSlug
  }

  function resolveSelectionFromBoss(bossSlug) {
    if (!bossSlug) {
      return
    }

    for (const version of versionOrder) {
      const raidsForVersion = getRaidsForVersion(raidCatalog, version)
      const raid = raidsForVersion.find((candidate) =>
        getRaidBossOptions(candidate).some((boss) => boss.slug === bossSlug)
      )

      if (raid) {
        selectedVersion = version
        selectedRaid = raid.slug
        selectedBoss = bossSlug
        return
      }
    }
  }

  function loadSelectionsFromQuery() {
    const params = new URLSearchParams(window.location.search)
    const queryEdition = params.get('edition')
    const queryRaid = params.get('raid')
    const queryBoss = params.get('boss')
    const querySpec = params.get('spec')
    const queryDifficulty = params.get('difficulty')
    const queryMetric = params.get('metric')

    if (queryEdition && versions.includes(queryEdition)) {
      selectedVersion = queryEdition
    }

    if (queryRaid) {
      selectedRaid = queryRaid
    }

    if (queryBoss) {
      selectedBoss = queryBoss
    }

    if (querySpec) {
      selectedSpec = querySpec
    }

    if (queryDifficulty && difficulties.includes(queryDifficulty)) {
      selectedDifficulty = queryDifficulty
    }

    if (queryMetric && metrics.includes(queryMetric)) {
      selectedMetric = queryMetric
    }
  }

  function syncSelectionsToQuery() {
    const url = new URL(window.location.href)

    url.searchParams.set('page', PAGE_QUERY_VALUE)

    if (selectedVersion) {
      url.searchParams.set('edition', selectedVersion)
    } else {
      url.searchParams.delete('edition')
    }

    // Cleanup legacy param to avoid duplicate semantics.
    url.searchParams.delete('version')

    if (selectedRaid) {
      url.searchParams.set('raid', selectedRaid)
    } else {
      url.searchParams.delete('raid')
    }

    if (selectedBoss) {
      url.searchParams.set('boss', selectedBoss)
    } else {
      url.searchParams.delete('boss')
    }

    if (selectedSpec) {
      url.searchParams.set('spec', selectedSpec)
    } else {
      url.searchParams.delete('spec')
    }

    if (selectedDifficulty) {
      url.searchParams.set('difficulty', selectedDifficulty)
    } else {
      url.searchParams.delete('difficulty')
    }

    if (selectedMetric) {
      url.searchParams.set('metric', selectedMetric)
    } else {
      url.searchParams.delete('metric')
    }

    const nextUrl = `${url.pathname}${url.search}${url.hash}`
    window.history.replaceState({}, '', nextUrl)
  }

  async function loadRankings() {
    if (!selectedBoss || !selectedSpec) {
      rankings = null
      return
    }

    const backendBoss = (worldData.bosses || []).find((boss) => boss.nameSlug === selectedBoss)
    if (!backendBoss) {
      rankings = null
      error = 'Rankings are not available for this raid yet.'
      return
    }

    loading = true
    error = ''

    try {
      const cacheKey = `rankings-${selectedSpec}-${selectedBoss}-${selectedDifficulty}-${selectedMetric}`
      rankings = Cache.getRankingsCache(cacheKey)

      if (!rankings) {
        rankings = await API.getSpecRankings(
          selectedSpec,
          selectedBoss,
          selectedDifficulty,
          selectedMetric
        )
        Cache.cacheRankings(cacheKey, rankings)
      }
    } catch (err) {
      error = err.message
      console.error('Failed to load rankings:', err)
      rankings = null
    } finally {
      loading = false
    }
  }

  $: if (initialSpec) selectedSpec = initialSpec

  onMount(() => {
    loadSelectionsFromQuery()

    if (!selectedBoss) {
      resolveSelectionFromBoss(initialBoss)
    }

    if (!selectedSpec && initialSpec) {
      selectedSpec = initialSpec
    }

    hasHydratedQuery = true
  })

  $: if (selectedBoss || selectedSpec || selectedDifficulty || selectedMetric) {
    loadRankings()
  }

  $: if (typeof window !== 'undefined' && hasHydratedQuery) {
    void selectedVersion
    void selectedRaid
    void selectedBoss
    void selectedSpec
    void selectedDifficulty
    void selectedMetric
    syncSelectionsToQuery()
  }
</script>

<div class="rankings-page">
  <h2>Spec Rankings</h2>
  <p class="subtitle">Filter by timeline, raid, and specialization to inspect top report cohorts.</p>

  <div class="selector-cards">
    <div class="selector-block">
      <h3>Choose Edition</h3>
      <EditionSelector
        {versions}
        {selectedVersion}
        {getVersionIconUrl}
        on:select={(event) => selectVersion(event.detail.version)}
      />
    </div>

    <RaidBossSelectors
      {raidPhaseGroups}
      {bosses}
      bind:selectedRaid
      bind:selectedBoss
      raidLabel="Raid"
      bossLabel="Boss"
      raidPlaceholder="Select a raid..."
      bossPlaceholder="Select a boss..."
    />
  </div>

  <div class="controls">
    <div class="control-group">
      <label>
        <span>Spec:</span>
        <select bind:value={selectedSpec}>
          <option value="">Select a spec...</option>
          {#each availableSpecs as spec (spec.fullNameSlug)}
            <option value={spec.fullNameSlug}>{spec.fullName}</option>
          {/each}
        </select>
      </label>
    </div>

    <div class="control-group">
      <label>
        <span>Difficulty:</span>
        <select bind:value={selectedDifficulty}>
          {#each difficulties as difficulty (difficulty)}
            <option value={difficulty}>{difficulty}</option>
          {/each}
        </select>
      </label>
    </div>

    <div class="control-group">
      <label>
        <span>Metric:</span>
        <select bind:value={selectedMetric}>
          {#each metrics as metric (metric)}
            <option value={metric}>{metric.toUpperCase()}</option>
          {/each}
        </select>
      </label>
    </div>
  </div>

  {#if availableSpecs.length}
    <div class="spec-quick-picks" aria-label="Spec quick picks">
      {#each availableSpecs as spec (spec.fullNameSlug)}
        <button
          class="spec-pill"
          class:active={selectedSpec === spec.fullNameSlug}
          style="--class-accent: {getSpecClassColor(spec)}"
          on:click={() => selectSpecFromGrid(spec.fullNameSlug)}
        >
          <img
            class="spec-icon"
            src={getSpecIconUrl(getSpecClassSlug(spec), spec.nameSlug)}
            alt={spec.fullName}
            loading="lazy"
          />
          <img
            class="class-icon"
            src={getClassIconUrl(getSpecClassSlug(spec))}
            alt={getSpecClass(spec)?.name || 'Class'}
            loading="lazy"
          />
          <span>{spec.fullName}</span>
        </button>
      {/each}
    </div>
  {/if}

  {#if selectedSpecMeta && selectedClassMeta}
    <div class="selection-summary" style="--class-accent: {getSpecClassColor(selectedSpecMeta)}">
      <img
        class="summary-spec"
        src={getSpecIconUrl(selectedClassMeta.nameSlug, selectedSpecMeta.nameSlug)}
        alt={selectedSpecMeta.fullName}
      />
      <div>
        <p class="label">Current Selection</p>
        <p class="value">{selectedSpecMeta.fullName} ({selectedClassMeta.name})</p>
      </div>
    </div>
  {/if}

  {#if error}
    <div class="error">
      <p><strong>Error:</strong> {error}</p>
    </div>
  {/if}

  {#if loading}
    <div class="loading">Loading rankings...</div>
  {:else if rankings}
    <RankingsList {rankings} />
  {:else if selectedBoss && selectedSpec}
    <div class="no-data">No rankings found for this selection</div>
  {:else}
    <div class="no-data">Select a boss and spec to view rankings</div>
  {/if}
</div>

<style lang="scss">
  @use '../styles/selection-tokens' as selectionTokens;

  .rankings-page {
    @include selectionTokens.apply-selection-tokens;

    max-width: 1120px;
    margin: 0 auto;
    animation: fadeIn 0.3s ease-in-out;
  }

  .subtitle {
    margin: -0.75rem 0 var(--space-7);
    color: var(--hero-subtitle);
  }

  @keyframes fadeIn {
    from {
      opacity: 0;
    }
    to {
      opacity: 1;
    }
  }

  h2 {
    margin: 0 0 1.5rem 0;
    font-size: 1.95rem;
    color: var(--hero-title);
  }

  .selector-cards {
    display: grid;
    gap: var(--space-7);
    margin-bottom: var(--space-7);
  }

  .selector-block {
    background: var(--panel-bg);
    border: 1px solid var(--panel-border);
    border-radius: 0.85rem;
    padding: 0.9rem;
  }

  .selector-block h3 {
    margin: 0 0 0.65rem;
    font-size: var(--meta-font-size);
    font-weight: 700;
    color: var(--section-heading);
    text-transform: uppercase;
    letter-spacing: 0.08em;
  }

  .controls {
    display: grid;
    grid-template-columns: repeat(3, minmax(0, 1fr));
    gap: var(--space-7);
    margin-bottom: var(--space-11);
    padding: var(--space-9);
    background: var(--panel-bg);
    border-radius: 0.85rem;
    border: 1px solid var(--panel-border);
    box-shadow: var(--panel-shadow);
  }

  .control-group label {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
  }

  .control-group span {
    font-weight: 600;
    font-size: 0.8rem;
    color: var(--section-heading);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .control-group select {
    appearance: none;
    -webkit-appearance: none;
    background: var(--select-bg);
    background-image: var(--select-chevron-icon);
    background-repeat: no-repeat;
    background-position: right var(--select-chevron-offset) center;
    background-size: var(--select-chevron-size);
    border: 1px solid var(--select-border);
    color: var(--select-text);
    padding: 0.5rem;
    padding-right: var(--select-padding-right);
    border-radius: 0.25rem;
    font-size: var(--control-font-size);
    cursor: pointer;
    transition:
      border-color 0.15s,
      box-shadow 0.15s,
      background-color 0.15s,
      color 0.15s;
  }

  .control-group select:hover:not(:disabled) {
    border-color: var(--select-hover-border);
    background-color: var(--select-hover-bg);
  }

  .control-group select:focus {
    outline: none;
    border-color: var(--select-focus-border);
    box-shadow: var(--select-focus-ring);
    background: var(--select-hover-bg);
  }

  .control-group select:disabled {
    background-color: var(--disabled-bg);
    background-image: var(--disabled-chevron-icon);
    background-repeat: no-repeat;
    background-position: right var(--select-chevron-offset) center;
    background-size: var(--select-chevron-size);
    border-color: var(--disabled-border);
    color: var(--disabled-text);
    -webkit-text-fill-color: var(--disabled-text);
    cursor: not-allowed;
    opacity: 1;
    box-shadow: var(--disabled-icon-ring);
  }

  .control-group select option {
    background: #ffffff;
    color: var(--select-text);
    padding: 0.5rem;
  }

  .control-group select option:checked {
    background: #d5e8f8;
    color: #15334f;
  }

  .spec-quick-picks {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(170px, 1fr));
    gap: 0.55rem;
    margin-bottom: 1rem;
  }

  .spec-pill {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    position: relative;
    background: var(--spec-bg);
    border: 1px solid var(--class-accent, #c2d8e7);
    border-radius: 0.55rem;
    color: color-mix(in srgb, var(--class-accent, #2a4b69) 78%, #1a2f44);
    padding: 0.45rem 0.56rem;
    cursor: pointer;
    text-align: left;
    transition:
      border-color 0.15s,
      transform 0.15s,
      background 0.15s;
  }

  .spec-pill:hover {
    border-color: var(--class-accent, #7ca6c2);
    background: var(--spec-hover-bg);
    transform: translateY(-1px);
    color: color-mix(in srgb, var(--class-accent, #16344d) 78%, #ffffff);
  }

  .spec-pill.active {
    border-color: var(--class-accent, #2a7e8e);
    background: var(--spec-hover-bg);
    color: color-mix(in srgb, var(--class-accent, #16344d) 74%, #ffffff);
    box-shadow: 0 7px 16px rgba(42, 126, 142, 0.15);
  }

  .spec-pill .spec-icon {
    width: 32px;
    height: 32px;
    border-radius: 0.45rem;
    object-fit: cover;
    border: 1px solid var(--spec-icon-border);
  }

  .spec-pill .class-icon {
    position: absolute;
    left: 1.7rem;
    bottom: 0.22rem;
    width: 15px;
    height: 15px;
    border-radius: 999px;
    border: 2px solid var(--class-icon-border);
    background: var(--class-icon-bg);
    object-fit: cover;
  }

  .spec-pill span {
    font-size: 0.83rem;
    font-weight: 600;
    line-height: 1.1;
  }

  .selection-summary {
    display: inline-flex;
    align-items: center;
    gap: 0.7rem;
    margin-bottom: 1rem;
    background: var(--summary-bg);
    border: 1px solid var(--summary-border);
    border-radius: 0.72rem;
    padding: 0.55rem 0.7rem;
  }

  .summary-spec {
    width: 34px;
    height: 34px;
    border-radius: 0.45rem;
    border: 1px solid var(--spec-icon-border);
  }

  .selection-summary .label {
    margin: 0;
    text-transform: uppercase;
    letter-spacing: 0.08em;
    font-size: 0.69rem;
    color: var(--summary-label);
    font-weight: 700;
  }

  .selection-summary .value {
    margin: 0.12rem 0 0;
    font-size: 0.91rem;
    font-weight: 600;
    color: var(--summary-value);
  }

  .error {
    background: #fef2f2;
    border: 1px solid #f3b4b4;
    color: #8a2929;
    padding: 1rem;
    border-radius: 0.75rem;
    margin-bottom: 1rem;
  }

  .loading,
  .no-data {
    background: var(--summary-bg);
    border: 1px dashed var(--panel-border);
    border-radius: 0.75rem;
    padding: 1rem;
    color: var(--summary-value);
  }

  @media (max-width: 700px) {
    .controls {
      grid-template-columns: 1fr;
    }

    .spec-quick-picks {
      grid-template-columns: 1fr 1fr;
    }
  }

  :global(.theme-dark) .subtitle {
    color: #97b8d4;
  }

  :global(.theme-dark) h2 {
    color: #e6f2ff;
  }

  :global(.theme-dark) .selector-block {
    background: linear-gradient(180deg, #13263a 0%, #102032 100%);
    border-color: #355472;
  }

  :global(.theme-dark) .selector-block h3,
  :global(.theme-dark) .control-group span {
    color: #8eb8da;
  }

  :global(.theme-dark) .controls {
    background: linear-gradient(180deg, #13263a 0%, #102032 100%);
    border-color: #355472;
    box-shadow:
      inset 0 1px 0 rgba(147, 193, 226, 0.09),
      0 12px 26px rgba(2, 10, 18, 0.38);
  }

  :global(.theme-dark) .control-group select {
    background-color: #13283d;
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='12' height='8' viewBox='0 0 12 8'%3E%3Cpath fill='%2394b5d1' d='M6 8 .8 1.7A1 1 0 0 1 2.2.3L6 4.2 9.8.3a1 1 0 1 1 1.4 1.4z'/%3E%3C/svg%3E");
    background-repeat: no-repeat;
    background-position: right 0.72rem center;
    background-size: 0.72rem;
    border-color: #3f6383;
    color: #d5e8f8;
  }

  :global(.theme-dark) .control-group select:hover:not(:disabled) {
    border-color: #5d89ad;
    background-color: #18324c;
  }

  :global(.theme-dark) .control-group select:focus {
    border-color: #8bddd6;
    box-shadow: 0 0 0 2px rgba(139, 221, 214, 0.22);
    background-color: #1a3651;
  }

  :global(.theme-dark) .control-group select:disabled {
    background-color: #101f30;
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='12' height='8' viewBox='0 0 12 8'%3E%3Cpath fill='%23577592' d='M6 8 .8 1.7A1 1 0 0 1 2.2.3L6 4.2 9.8.3a1 1 0 1 1 1.4 1.4z'/%3E%3C/svg%3E");
    background-repeat: no-repeat;
    background-position: right 0.72rem center;
    background-size: 0.72rem;
    border-color: #2d4860;
    color: #6f8ca6;
    cursor: not-allowed;
    opacity: 1;
    box-shadow: inset 0 0 0 1px rgba(111, 140, 166, 0.18);
  }

  :global(.theme-dark) .control-group select option {
    background: #13283d;
    color: #d5e8f8;
  }

  :global(.theme-dark) .control-group select option:checked {
    background: #1f3f5f;
    color: #eef8ff;
  }

  :global(.theme-dark) .spec-pill {
    background: #162a3f;
    border-color: color-mix(in srgb, var(--class-accent, #3f6383) 68%, #3f6383);
    color: color-mix(in srgb, var(--class-accent, #d5e8f8) 80%, #c7dbed);
  }

  :global(.theme-dark) .spec-pill:hover {
    background: #1a324b;
    border-color: var(--class-accent, #76a0c5);
    color: color-mix(in srgb, var(--class-accent, #ecf7ff) 82%, #f3faff);
  }

  :global(.theme-dark) .spec-pill.active {
    border-color: var(--class-accent, #8bddd6);
    color: color-mix(in srgb, var(--class-accent, #ecf7ff) 78%, #e6f4ff);
    box-shadow: 0 7px 16px rgba(4, 14, 22, 0.4);
  }

  :global(.theme-dark) .spec-pill .spec-icon,
  :global(.theme-dark) .summary-spec {
    border-color: #4f7493;
  }

  :global(.theme-dark) .spec-pill .class-icon {
    border-color: #162a3f;
    background: #162a3f;
  }

  :global(.theme-dark) .selection-summary {
    background: #13263a;
    border-color: color-mix(in srgb, var(--class-accent, #355472) 70%, #355472);
  }

  :global(.theme-dark) .selection-summary .label {
    color: #8eb8da;
  }

  :global(.theme-dark) .selection-summary .value {
    color: #e6f2ff;
  }

  :global(.theme-dark) .loading,
  :global(.theme-dark) .no-data {
    background: #13263a;
    border-color: #355472;
    color: #a6c5df;
  }
</style>
