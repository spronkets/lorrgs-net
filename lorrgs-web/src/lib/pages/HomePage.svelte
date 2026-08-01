<script lang="ts">
  import { onMount } from 'svelte'
  import { getVersionIconUrl } from '../selectionIcons'
  import EditionSelector from '../components/EditionSelector.svelte'
  import RaidBossSelectors from '../components/RaidBossSelectors.svelte'
  import { getBlizzardClassColor } from '../classColors'
  import { getClassIconUrl, getSpecIconUrl } from '../wowAssets'
  import {
    getAvailableSpecsForVersion,
    getRaidBossOptions,
    getRaidsForVersion,
    groupRaidsByPhase
  } from '../raidCatalog'

  export let worldData = {}
  export let raidCatalog = {
    editions: [],
    instances: {}
  }
  export let onSelectSpec = () => {}

  let selectedVersion = ''
  let selectedRaid = ''
  let selectedBoss = ''
  let hasHydratedQuery = false

  const versionOrder = ['Anniversary', 'Mists of Pandaria', 'Era', 'Retail']
  const versions = versionOrder

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

  function onVersionChange() {
    selectedRaid = ''
    selectedBoss = ''
  }

  function selectVersion(version) {
    if (selectedVersion === version) return
    selectedVersion = version
    onVersionChange()
  }

  // Group specs by roleId, then map to role objects
  $: versionSpecs = getAvailableSpecsForVersion(worldData.specs || [], selectedVersion)
  $: classesById = new Map((worldData.classes || []).map((cls) => [Number(cls.id), cls]))
  $: activeSpecs = versionSpecs.length
  $: activeBossCount = bosses.filter((boss) => boss.mapped).length
  $: raidCount = raids.length
  $: specsByRole = (worldData.roles || [])
    .map((role) => ({
      role,
      specs: versionSpecs.filter((s) => s.roleId === role.id)
    }))
    .filter((g) => g.specs.length > 0)

  function selectSpec(spec) {
    if (!selectedBoss) return
    onSelectSpec(spec.fullNameSlug, selectedBoss)
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

  function loadSelectionsFromQuery() {
    const params = new URLSearchParams(window.location.search)
    const queryEdition = params.get('edition')
    const queryRaid = params.get('raid')
    const queryBoss = params.get('boss')

    if (queryEdition && versions.includes(queryEdition)) {
      selectedVersion = queryEdition
    }

    if (queryRaid) {
      selectedRaid = queryRaid
    }

    if (queryBoss) {
      selectedBoss = queryBoss
    }
  }

  function syncSelectionsToQuery() {
    const url = new URL(window.location.href)

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

    const nextUrl = `${url.pathname}${url.search}${url.hash}`
    window.history.replaceState({}, '', nextUrl)
  }

  onMount(() => {
    loadSelectionsFromQuery()
    hasHydratedQuery = true
  })

  $: if (typeof window !== 'undefined' && hasHydratedQuery) {
    void selectedVersion
    void selectedRaid
    void selectedBoss
    syncSelectionsToQuery()
  }
</script>

<div class="home-page">
  <div class="summary-grid">
    <article class="summary-card">
      <p class="label">Active Raids</p>
      <p class="value">{raidCount}</p>
      <p class="hint">Across {selectedVersion || 'selected timeline'} catalog</p>
    </article>
    <article class="summary-card">
      <p class="label">Supported Bosses</p>
      <p class="value">{activeBossCount}</p>
      <p class="hint">Ready for ranking pulls</p>
    </article>
    <article class="summary-card">
      <p class="label">Playable Specs</p>
      <p class="value">{activeSpecs}</p>
      <p class="hint">Edition-filtered specialization list</p>
    </article>
  </div>

  <div class="selector-section selector-block">
    <h3>Edition</h3>
    <EditionSelector
      {versions}
      {selectedVersion}
      {getVersionIconUrl}
      on:select={(event) => selectVersion(event.detail.version)}
    />
  </div>

  <div class="selector-section selector-controls">
    <RaidBossSelectors
      {raidPhaseGroups}
      {bosses}
      bind:selectedRaid
      bind:selectedBoss
      raidLabel="Raid"
      bossLabel="Boss"
      raidPlaceholder="— choose a raid —"
      bossPlaceholder="— choose a boss —"
    />
  </div>

  {#each specsByRole as { role, specs } (role.id)}
    <div class="role-section">
      <h3 class="role-header">
        {role.name}
      </h3>
      <div class="spec-grid">
        {#each specs as spec (spec.fullNameSlug)}
          <button
            class="spec-card"
            class:disabled={!selectedBoss}
            style="--class-accent: {getSpecClassColor(spec)}"
            on:click={() => selectSpec(spec)}
            title={selectedBoss ? `View ${spec.fullName}` : 'Select a boss first'}
          >
            <div class="spec-media">
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
            </div>
            <span class="spec-name">{spec.fullName}</span>
          </button>
        {/each}
      </div>
    </div>
  {/each}
</div>

<style lang="scss">
  @use '../styles/selection-tokens' as selectionTokens;

  .home-page {
    @include selectionTokens.apply-selection-tokens;

    max-width: 1120px;
    margin: 0 auto;
  }

  .summary-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
    gap: var(--space-5);
    margin-bottom: var(--selector-block-gap);
  }

  .summary-card {
    background: var(--summary-bg);
    border: 1px solid var(--summary-border);
    border-radius: var(--radius-xl);
    padding: var(--space-5) var(--space-6);
    box-shadow: var(--summary-shadow);
  }

  .summary-card .label {
    margin: 0;
    font-size: 0.76rem;
    text-transform: uppercase;
    letter-spacing: 0.09em;
    color: var(--summary-label);
    font-weight: 700;
  }

  .summary-card .value {
    margin: 0.15rem 0;
    font-size: 1.65rem;
    font-weight: 700;
    color: var(--summary-value);
  }

  .summary-card .hint {
    margin: 0;
    font-size: 0.84rem;
    color: var(--summary-hint);
  }

  .selector-section {
    margin-bottom: var(--space-8);
  }

  .selector-controls {
    margin-bottom: var(--selector-block-gap);
  }

  .selector-controls :global(.raid-boss-row) {
    width: 100%;
  }

  .selector-block h3 {
    font-size: 1rem;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.08em;
    margin: 0 0 var(--space-4);
    padding-bottom: 0;
    border-bottom: none;
    color: inherit;
  }

  .selector-controls :global(label) {
    color: inherit;
    gap: var(--space-3);
  }

  .selector-controls :global(label > span) {
    font-size: 1rem;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.08em;
    color: inherit;
  }


  .role-section {
    margin-bottom: var(--space-11);
  }

  .role-header {
    font-size: 1rem;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.08em;
    margin: 0 0 0.75rem;
    padding-bottom: 0;
  }

  .spec-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(170px, 1fr));
    gap: var(--space-3);
  }

  .spec-card {
    display: flex;
    align-items: center;
    gap: 0.55rem;
    padding: var(--select-padding-y) var(--select-padding-x);
    background: var(--spec-bg);
    border: 1px solid var(--class-accent, #c2d8e7);
    border-radius: var(--radius-md);
    color: color-mix(in srgb, var(--class-accent, #2a4b69) 78%, #1a2f44);
    cursor: pointer;
    font-size: 0.875rem;
    transition:
      background var(--transition-fast),
      border-color var(--transition-fast),
      transform var(--transition-fast);
  }

  .spec-card:hover:not(.disabled) {
    background: var(--spec-hover-bg);
    border-color: var(--class-accent, #7ca6c2);
    color: color-mix(in srgb, var(--class-accent, #16344d) 78%, #ffffff);
    transform: translateY(-1px);
    box-shadow: 0 0 0 1px color-mix(in srgb, var(--class-accent, #7ca6c2) 30%, transparent);
  }

  .spec-card.disabled {
    background: linear-gradient(160deg, var(--disabled-bg), var(--disabled-bg-alt));
    border-color: var(--disabled-border);
    color: var(--disabled-text);
    cursor: not-allowed;
    opacity: 1;
    box-shadow: var(--disabled-icon-ring);
  }

  .spec-card.disabled .spec-icon {
    border-color: var(--disabled-border);
    filter: grayscale(0.64) saturate(0.28) brightness(0.95);
  }

  .spec-card.disabled .class-icon {
    border-color: var(--disabled-icon-bg);
    background: var(--disabled-icon-bg);
    filter: grayscale(0.64) saturate(0.28) brightness(0.95);
  }

  .spec-card.disabled .spec-name {
    color: var(--disabled-text);
    font-weight: 500;
  }

  .spec-media {
    position: relative;
    width: 36px;
    height: 36px;
    flex-shrink: 0;
  }

  .spec-icon {
    width: var(--icon-size);
    height: var(--icon-size);
    border-radius: var(--radius-sm);
    object-fit: cover;
    border: 1px solid var(--spec-icon-border);
  }

  .class-icon {
    position: absolute;
    width: var(--class-icon-size);
    height: var(--class-icon-size);
    border-radius: 999px;
    object-fit: cover;
    right: -4px;
    bottom: -4px;
    border: 2px solid var(--class-icon-border);
    background: var(--class-icon-bg);
  }

  .spec-name {
    text-align: left;
    line-height: 1.2;
    font-weight: 600;
    color: inherit;
  }

  @media (max-width: 700px) {
    .spec-grid {
      grid-template-columns: 1fr 1fr;
    }

  }

  :global(.theme-dark) .spec-card.disabled {
    background: linear-gradient(160deg, var(--disabled-bg), var(--disabled-bg-alt));
    border-color: var(--disabled-border);
    color: var(--disabled-text);
    cursor: not-allowed;
    opacity: 1;
    box-shadow: var(--disabled-icon-ring);
  }

  :global(.theme-dark) .spec-card.disabled .spec-icon {
    border-color: var(--disabled-border);
    filter: grayscale(0.68) saturate(0.28) brightness(0.85);
  }

  :global(.theme-dark) .spec-card.disabled .class-icon {
    border-color: var(--disabled-icon-bg);
    background: var(--disabled-icon-bg);
    filter: grayscale(0.68) saturate(0.28) brightness(0.85);
  }

  :global(.theme-dark) .spec-card.disabled .spec-name {
    color: var(--disabled-text);
    font-weight: 500;
  }
</style>
