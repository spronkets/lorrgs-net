<script lang="ts">
  import { onMount, tick } from 'svelte';
  import * as API from '../api';
  import {
    type SpecRankingData,
    type WorldDataSnapshot,
    type WowClass,
    type WowRole,
    type WowSpec
  } from '../api';
  import { getVersionIconUrl } from '../selectionIcons';
  import EditionSelector from '../components/EditionSelector.svelte';
  import RaidBossSelectors from '../components/RaidBossSelectors.svelte';
  import RankingsList from '../components/RankingsList.svelte';
  import { getBlizzardClassColor } from '../classColors';
  import { getClassIconUrl, getSpecIconUrl } from '../wowAssets';
  import {
    getApiEditionSlug,
    getAvailableSpecsForVersion,
    type RaidCatalog,
    getRaidBossOptions,
    getRaidsForEdition,
    groupRaidsByPhase
  } from '../raidCatalog';

  export let worldData: WorldDataSnapshot = {
    roles: [],
    classes: [],
    specs: []
  };
  export let raidCatalog: RaidCatalog = {
    editions: [],
    instances: {}
  };

  let selectedVersion = '';
  let selectedRaid = '';
  let selectedBoss = '';
  let selectedSpec = '';
  let rankings: SpecRankingData | null = null;
  let rankingsLoading = false;
  let rankingsError = '';
  let isSelectionCollapsed = false;
  let lastRankingsRequestKey = '';
  let hasHydratedQuery = false;

  const versionOrder = ['Anniversary', 'Mists of Pandaria', 'Era', 'Retail'];
  const versions = versionOrder;

  $: raids = getRaidsForEdition(raidCatalog, selectedVersion);
  $: raidPhaseGroups = groupRaidsByPhase(raids);
  $: if (!selectedVersion && versions.length) {
    selectedVersion =
      versions.find((version) => getRaidsForEdition(raidCatalog, version).length > 0) ||
      versions[0];
  }
  $: selectedRaidOption = raids.find((raid) => raid.slug === selectedRaid);
  $: bosses = getRaidBossOptions(selectedRaidOption);

  $: if (selectedRaid && !raids.some((raid) => raid.slug === selectedRaid)) {
    selectedRaid = '';
  }

  $: if (selectedBoss && !bosses.some((boss) => boss.slug === selectedBoss)) {
    selectedBoss = '';
  }

  function onVersionChange() {
    selectedRaid = '';
    selectedBoss = '';
  }

  function selectVersion(version: string) {
    if (selectedVersion === version) return;
    selectedVersion = version;
    onVersionChange();
  }

  // Group specs by roleId, then map to role objects
  $: versionSpecs = getAvailableSpecsForVersion(worldData.specs || [], selectedVersion);
  $: classesById = new Map((worldData.classes || []).map((cls) => [Number(cls.id), cls]));
  $: rolesById = new Map((worldData.roles || []).map((role) => [Number(role.id), role]));
  $: activeSpecs = versionSpecs.length;
  $: activeBossCount = bosses.filter((boss) => boss.mapped).length;
  $: raidCount = raids.length;
  $: selectedSpecMeta = versionSpecs.find((spec) => spec.fullNameSlug === selectedSpec);
  $: selectedClassMeta = selectedSpecMeta ? getSpecClass(selectedSpecMeta) : null;
  $: selectedBossMeta = bosses.find((boss) => boss.slug === selectedBoss);
  $: selectedWorldBoss = (worldData.bosses || []).find((boss) => boss.nameSlug === selectedBoss);
  $: selectedEditionSlug = getApiEditionSlug(selectedVersion);
  $: supportsDifficulty = selectedVersion === 'Retail' || selectedVersion === 'Mists of Pandaria';
  $: selectedMetric = selectedSpecMeta ? resolveMetricForSpec(selectedSpecMeta) : 'dps';
  $: selectedDifficulty = supportsDifficulty
    ? selectedVersion === 'Retail'
      ? 'Mythic'
      : 'Normal'
    : 'Normal';
  $: selectedZoneId = selectedRaidOption?.zoneId || selectedWorldBoss?.raidId || 0;
  $: selectedEncounterId = selectedBossMeta?.id || selectedWorldBoss?.id || 0;
  $: selectedDifficultyId = selectedDifficulty === 'Mythic'
    ? 5
    : selectedDifficulty === 'Heroic'
      ? 4
      : selectedDifficulty === 'Normal'
        ? 3
        : selectedDifficulty === 'LFR'
          ? 2
          : 0;
  $: specsByRole = (worldData.roles || [])
    .map((role) => ({
      role,
      specs: versionSpecs.filter((s) => s.roleId === role.id)
    }))
    .filter((g) => g.specs.length > 0);

  $: if (selectedSpec && !versionSpecs.some((spec) => spec.fullNameSlug === selectedSpec)) {
    selectedSpec = '';
  }

  $: if (!selectedBoss && selectedSpec) {
    selectedSpec = '';
  }

  $: if (!selectedSpec) {
    isSelectionCollapsed = false;
  }

  function selectSpec(spec: WowSpec) {
    if (!selectedBoss) return;
    selectedSpec = spec.fullNameSlug;
    isSelectionCollapsed = true;
  }

  function expandSelections() {
    isSelectionCollapsed = false;
  }

  function resolveMetricForSpec(spec: WowSpec) {
    const role = rolesById.get(Number(spec?.roleId)) as WowRole | undefined;
    const metric = String(role?.metric || role?.Metric || '')
      .trim()
      .toLowerCase();
    return metric === 'hps' ? 'hps' : 'dps';
  }

  function getSpecClass(spec: WowSpec): WowClass | null {
    return (classesById.get(Number(spec.classId)) as WowClass | undefined) || null;
  }

  function getSpecClassSlug(spec: WowSpec): string {
    const matchedClass = getSpecClass(spec);
    return matchedClass?.nameSlug || 'other';
  }

  function getSpecClassColor(spec: WowSpec): string {
    const matchedClass = getSpecClass(spec);
    return getBlizzardClassColor(matchedClass?.nameSlug, matchedClass?.color || '#7BA4BF');
  }

  function loadSelectionsFromQuery() {
    const params = new URLSearchParams(window.location.search);
    const queryEdition = params.get('edition');
    const queryRaid = params.get('raid');
    const queryBoss = params.get('boss');
    const querySpec = params.get('spec');

    if (queryEdition && versions.includes(queryEdition)) {
      selectedVersion = queryEdition;
    }

    if (queryRaid) {
      selectedRaid = queryRaid;
    }

    if (queryBoss) {
      selectedBoss = queryBoss;
    }

    if (querySpec) {
      selectedSpec = querySpec;
    }

    return Boolean(queryBoss && querySpec);
  }

  function syncSelectionsToQuery() {
    const url = new URL(window.location.href);

    if (selectedVersion) {
      url.searchParams.set('edition', selectedVersion);
    } else {
      url.searchParams.delete('edition');
    }

    // Cleanup legacy param to avoid duplicate semantics.
    url.searchParams.delete('version');

    if (selectedRaid) {
      url.searchParams.set('raid', selectedRaid);
    } else {
      url.searchParams.delete('raid');
    }

    if (selectedBoss) {
      url.searchParams.set('boss', selectedBoss);
    } else {
      url.searchParams.delete('boss');
    }

    if (selectedSpec) {
      url.searchParams.set('spec', selectedSpec);
    } else {
      url.searchParams.delete('spec');
    }

    url.searchParams.delete('size');

    const nextUrl = `${url.pathname}${url.search}${url.hash}`;
    window.history.replaceState({}, '', nextUrl);
  }

  async function loadRankings(forceRefresh = false) {
    if (!selectedBoss || !selectedSpec) {
      rankings = null;
      rankingsError = '';
      lastRankingsRequestKey = '';
      return;
    }

    const requestKey = [
      selectedEditionSlug,
      selectedSpec,
      selectedBoss,
      selectedDifficulty,
      selectedMetric
    ].join('|');

    if (!forceRefresh && requestKey === lastRankingsRequestKey) {
      return;
    }

    lastRankingsRequestKey = requestKey;
    rankingsLoading = true;
    rankingsError = '';

    if (!selectedZoneId) {
      rankings = null;
      rankingsError = 'Selected raid boss is missing zone mapping. Please choose a mapped raid.';
      rankingsLoading = false;
      return;
    }

    try {
      rankings = await API.getSpecRankings(
        selectedSpec,
        selectedBoss,
        selectedDifficulty,
        selectedMetric,
        selectedEditionSlug,
        {
          zoneId: selectedZoneId || undefined,
          encounterId: selectedEncounterId || undefined,
          className: selectedClassMeta?.name || undefined,
          specName: selectedSpecMeta?.name || undefined,
          difficultyId: selectedDifficultyId || undefined
        }
      );
    } catch (err) {
      rankings = null;
      rankingsError = err instanceof Error ? err.message : 'Failed to load rankings.';
      console.error('Failed to load rankings:', err);
    } finally {
      rankingsLoading = false;
    }
  }

  onMount(async () => {
    const shouldAutoLoad = loadSelectionsFromQuery();
    isSelectionCollapsed = shouldAutoLoad;
    hasHydratedQuery = true;

    // Wait one microtask so derived selections settle before loading.
    await tick();

    if (shouldAutoLoad && selectedBoss && selectedSpec) {
      void loadRankings();
    }
  });

  $: if (hasHydratedQuery && selectedBoss && selectedSpec) {
    void loadRankings();
  }

  $: if (typeof window !== 'undefined' && hasHydratedQuery) {
    void selectedVersion;
    void selectedRaid;
    void selectedBoss;
    void selectedSpec;
    syncSelectionsToQuery();
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

  {#if isSelectionCollapsed && selectedSpecMeta && selectedBoss}
    <div class="selection-collapsed" style="--class-accent: {getSpecClassColor(selectedSpecMeta)}">
      <p class="label">Current Selection</p>
      <div class="selection-overview">
        <div class="spec-media selection-spec-media">
          <img
            class="spec-icon"
            src={getSpecIconUrl(getSpecClassSlug(selectedSpecMeta), selectedSpecMeta.nameSlug)}
            alt={selectedSpecMeta.fullName}
            loading="lazy"
          />
          <img
            class="class-icon"
            src={getClassIconUrl(getSpecClassSlug(selectedSpecMeta))}
            alt={selectedClassMeta?.name || 'Class'}
            loading="lazy"
          />
        </div>
        <div>
          <p class="value">
            {selectedVersion} / {selectedRaidOption?.name || selectedRaid} / {selectedBossMeta?.name ||
              selectedBoss}
          </p>
          <p class="value spec-value">{selectedSpecMeta.fullName}</p>
        </div>
      </div>
      <button class="edit-selection" type="button" on:click={expandSelections}
        >Change selection</button
      >
    </div>
  {:else}
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
              class:active={selectedSpec === spec.fullNameSlug}
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

  {/if}

  {#if isSelectionCollapsed}
    {#if rankingsLoading}
      <div class="loading">Loading rankings...</div>
    {:else if rankingsError}
      <div class="error">
        <p><strong>Error:</strong> {rankingsError}</p>
      </div>
    {:else if rankings}
      <RankingsList
        {rankings}
        specLabel={selectedSpecMeta?.fullName || ''}
        bossLabel={selectedBossMeta?.name || ''}
      />
    {:else if selectedBoss && selectedSpec && lastRankingsRequestKey}
      <div class="no-data">No rankings found for this selection</div>
    {/if}
  {/if}
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

  .ranking-size-control {
    margin-bottom: var(--space-5);
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    max-width: 220px;
  }

  .ranking-size-control label {
    font-size: 1rem;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.08em;
  }

  .ranking-size-control select {
    appearance: none;
    -webkit-appearance: none;
    width: 100%;
    min-width: 0;
    padding: var(--select-padding-y, 0.52rem) var(--select-padding-x, 0.62rem);
    padding-right: var(--select-padding-right, 2.1rem);
    background: var(--select-bg);
    background-image: var(--select-chevron-icon);
    background-repeat: no-repeat;
    background-position: right var(--select-chevron-offset, 0.72rem) center;
    background-size: var(--select-chevron-size, 0.72rem);
    border: 1px solid var(--select-border);
    color: var(--select-text);
    border-radius: var(--radius-md, 0.58rem);
    font-size: var(--control-font-size, 0.95rem);
    transition:
      border-color var(--transition-fast, 0.15s),
      box-shadow var(--transition-fast, 0.15s),
      background-color var(--transition-fast, 0.15s),
      color var(--transition-fast, 0.15s);
  }

  .ranking-size-control select:hover {
    border-color: var(--select-hover-border);
    background-color: var(--select-hover-bg);
    box-shadow: var(--select-hover-ring);
  }

  .ranking-size-control select:focus {
    outline: none;
    border-color: var(--select-focus-border);
    box-shadow: var(--select-focus-ring);
    background-color: var(--select-hover-bg);
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
    width: 100%;
    border: 1px solid
      color-mix(in srgb, var(--class-accent, var(--select-border)) 22%, var(--select-border));
    border-radius: var(--radius-md);
    background: var(--spec-bg);
    color: color-mix(in srgb, var(--summary-value) 72%, var(--class-accent, var(--summary-value)) 28%);
    display: flex;
    align-items: center;
    gap: 0.65rem;
    padding: 0.48rem 0.52rem;
    text-align: left;
    font-size: 0.94rem;
    cursor: pointer;
    transition:
      border-color var(--transition-fast),
      background-color var(--transition-fast),
      box-shadow var(--transition-fast),
      transform var(--transition-fast);
  }

  .spec-card:hover:not(.disabled) {
    border-color: color-mix(in srgb, var(--class-accent, var(--select-hover-border)) 48%, var(--select-hover-border));
    background: var(--spec-hover-bg);
    box-shadow: var(--spec-hover-ring);
    transform: translateY(-1px);
  }

  .spec-card.active {
    border-color: color-mix(in srgb, var(--class-accent, var(--select-focus-border)) 72%, #ffffff 28%);
    background: color-mix(in srgb, var(--class-accent, var(--select-hover-bg)) 12%, var(--spec-hover-bg));
    box-shadow:
      0 0 0 1px color-mix(in srgb, var(--class-accent, var(--select-focus-border)) 42%, transparent),
      var(--select-focus-ring);
  }


  .spec-card.disabled {
    background: linear-gradient(160deg, var(--disabled-bg), var(--disabled-bg-alt));
    border-color: var(--disabled-border);
    color: var(--disabled-text);
    cursor: not-allowed;
    opacity: 0.88;
    box-shadow:
      inset 0 0 0 1px color-mix(in srgb, var(--disabled-border) 75%, #ffffff 25%),
      0 1px 2px rgba(67, 88, 111, 0.12);
  }

  .spec-card.disabled .spec-icon {
    border-color: var(--disabled-border);
    filter: grayscale(0.78) saturate(0.2) brightness(0.9);
  }

  .spec-card.disabled .class-icon {
    border-color: var(--disabled-icon-bg);
    background: var(--disabled-icon-bg);
    filter: grayscale(0.78) saturate(0.2) brightness(0.9);
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
    color: color-mix(in srgb, currentColor 82%, var(--class-accent, currentColor) 18%);
  }

  .spec-card.active .spec-name {
    color: color-mix(in srgb, currentColor 62%, var(--class-accent, currentColor) 38%);
  }

  .selection-collapsed {
    background: var(--summary-bg);
    border: 1px solid
      color-mix(in srgb, var(--class-accent, var(--summary-border)) 48%, var(--summary-border));
    border-radius: var(--radius-xl);
    box-shadow:
      var(--summary-shadow),
      0 0 0 1px color-mix(in srgb, var(--class-accent, #7ca6c2) 24%, transparent);
    padding: var(--space-5) var(--space-6);
    margin-bottom: var(--selector-block-gap);
    display: grid;
    gap: var(--space-2);
  }

  .selection-actions {
    margin: var(--space-8) 0 var(--selector-block-gap);
    display: flex;
    justify-content: flex-start;
  }

  .load-rankings {
    padding: 0.55rem 0.95rem;
    border-radius: var(--radius-md);
    border: 1px solid var(--select-focus-border);
    background: color-mix(in srgb, var(--select-focus-border) 18%, var(--select-bg));
    color: var(--select-text);
    font-size: 0.9rem;
    font-weight: 700;
    letter-spacing: 0.02em;
    cursor: pointer;
    transition:
      border-color var(--transition-fast),
      background-color var(--transition-fast),
      box-shadow var(--transition-fast),
      color var(--transition-fast);
  }

  .load-rankings:hover:not(:disabled) {
    border-color: var(--select-hover-border);
    background: var(--select-hover-bg);
    box-shadow: var(--select-hover-ring);
  }

  .load-rankings:disabled {
    background: var(--disabled-bg);
    border-color: var(--disabled-border);
    color: var(--disabled-text);
    cursor: not-allowed;
    box-shadow: none;
  }

  .selection-overview {
    display: flex;
    align-items: center;
    gap: 0.7rem;
  }

  .selection-spec-media {
    width: 40px;
    height: 40px;
  }

  .selection-collapsed .label {
    margin: 0;
    font-size: 0.76rem;
    text-transform: uppercase;
    letter-spacing: 0.09em;
    color: var(--summary-label);
    font-weight: 700;
  }

  .selection-collapsed .value {
    margin: 0;
    font-size: 0.95rem;
    color: color-mix(
      in srgb,
      var(--summary-value) 70%,
      var(--class-accent, var(--summary-value)) 30%
    );
    font-weight: 600;
  }

  .selection-collapsed .spec-value {
    margin-top: 0.12rem;
    color: color-mix(in srgb, var(--class-accent, var(--summary-value)) 72%, #ffffff 28%);
  }

  .edit-selection {
    justify-self: start;
    margin-top: var(--space-2);
    padding: 0.45rem 0.7rem;
    border-radius: var(--radius-md);
    border: 1px solid var(--select-border);
    background: var(--select-bg);
    color: var(--select-text);
    cursor: pointer;
    font-size: 0.85rem;
    font-weight: 600;
  }

  .edit-selection:hover {
    border-color: var(--select-hover-border);
    background: var(--select-hover-bg);
    box-shadow: var(--select-hover-ring);
  }

  .loading,
  .no-data,
  .error {
    margin-bottom: var(--space-7);
  }

  .loading,
  .no-data {
    background: var(--summary-bg);
    border: 1px dashed var(--panel-border);
    border-radius: 0.75rem;
    padding: 1rem;
    color: var(--summary-value);
  }

  .error {
    background: #fef2f2;
    border: 1px solid #f3b4b4;
    color: #8a2929;
    padding: 1rem;
    border-radius: 0.75rem;
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
