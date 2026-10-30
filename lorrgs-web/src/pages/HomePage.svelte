<script lang="ts">
  import Button from '../lib/components/Button.svelte';
  import Alert from '../lib/components/Alert.svelte';
  import Spinner from '../lib/components/Spinner.svelte';
  import { onMount, tick } from 'svelte';
  import * as API from '../lib/api';
  import type { SpecRankingData } from '../lib/models/SpecRankingData';
  import type { WorldDataSnapshot } from '../lib/models/WorldDataSnapshot';
  import type { WowClass } from '../lib/models/WowClass';
  import type { WowRole } from '../lib/models/WowRole';
  import type { WowSpec } from '../lib/models/WowSpec';
  import EncounterFilters from '../lib/components/EncounterFilters.svelte';
  import RankingPageHeader from '../lib/components/RankingPageHeader.svelte';
  import RankingsList from '../lib/components/RankingsList.svelte';
  import BossIcon from '../lib/components/BossIcon.svelte';
  import { getBlizzardClassColor } from '../lib/classColors';
  import { getClassIconUrl, getSpecIconUrl } from '../lib/wowAssets';
  import {
    getApiEditionSlug,
    getAvailableSpecsForVersion,
    getRaidBossOptions,
    getRaidsForEdition
  } from '../lib/raidCatalog';
  import type { RaidCatalog } from '../lib/models/RaidCatalog';

  export let worldData: WorldDataSnapshot = {
    roles: [],
    classes: [],
    specs: []
  };
  export let raidCatalog: RaidCatalog = {
    editions: [],
    instances: {}
  };

  export let selectedVersion = '';
  export let oneditionchange: ((_version: string) => void) | undefined = undefined;
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
  $: if (!selectedVersion && versions.length) {
    selectedVersion =
      versions.find((version) => getRaidsForEdition(raidCatalog, version).length > 0) ||
      versions[0];
  }
  $: selectedRaidOption = raids.find(
    (raid) => raid.slug === selectedRaid && raid.isAvailable !== false
  );
  $: bosses = getRaidBossOptions(selectedRaidOption);
  $: bossOptions = bosses.map((boss) => {
    const metadata = worldData.bosses?.find((entry) => entry.nameSlug === boss.slug);
    return {
      ...boss,
      iconName: metadata?.icon
    };
  });
  $: selectedBossIconName = bossOptions.find((boss) => boss.slug === selectedBoss)?.iconName;

  $: if (
    selectedRaid &&
    !raids.some((raid) => raid.slug === selectedRaid && raid.isAvailable !== false)
  ) {
    selectedRaid = '';
  }

  $: if (selectedBoss && !bosses.some((boss) => boss.slug === selectedBoss)) {
    selectedBoss = '';
  }

  // Group specs by roleId, then map to role objects
  $: versionSpecs = getAvailableSpecsForVersion(worldData.specs || [], selectedVersion);
  $: classesById = new Map((worldData.classes || []).map((cls) => [Number(cls.id), cls]));
  $: rolesById = new Map((worldData.roles || []).map((role) => [Number(role.id), role]));
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
  $: selectedDifficultyId =
    selectedDifficulty === 'Mythic'
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
          difficultyId: selectedDifficultyId || undefined,
          includeOtherPlayers: false
        }
      );
    } catch (err: unknown) {
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

<div class="home-page" class:comparison-open={isSelectionCollapsed}>
  <RankingPageHeader
    title="Spec Rankings"
    description="Compare cooldown usage across ranked players of a chosen specialization."
  />
  {#if isSelectionCollapsed && selectedSpecMeta && selectedBoss}
    <div class="selection-collapsed" style="--class-accent: {getSpecClassColor(selectedSpecMeta)}">
      <p class="label">Current Selection</p>
      <div class="selection-overview">
        <div>
          <p class="value">
            {selectedVersion} / {selectedRaidOption?.name || selectedRaid}
          </p>
          <h2 class="selection-title">
            <span class="selected-spec">
              <span class="spec-media selection-spec-media">
                <img
                  class="spec-icon"
                  src={getSpecIconUrl(
                    getSpecClassSlug(selectedSpecMeta),
                    selectedSpecMeta.nameSlug
                  )}
                  alt=""
                />
                <img
                  class="class-icon"
                  src={getClassIconUrl(getSpecClassSlug(selectedSpecMeta))}
                  alt={selectedClassMeta?.name || 'Class'}
                />
              </span>
              <span class="spec-value">{selectedSpecMeta.fullName}</span>
            </span>
            <span class="versus">vs.</span>
            <span class="selected-boss">
              <BossIcon slug={selectedBoss} iconName={selectedBossIconName} size={38} />
              <span>{selectedBossMeta?.name || selectedBoss}</span>
            </span>
          </h2>
        </div>
      </div>
      <Button
        variant="secondary-outline"
        size="small"
        class={['home-page-button', `edit-selection`]}
        type="button"
        onclick={expandSelections}>Change selection</Button
      >
    </div>
  {:else}
    <EncounterFilters
      {worldData}
      {raidCatalog}
      bind:selectedVersion
      bind:selectedRaid
      bind:selectedBoss
      {oneditionchange}
    />
    {#each specsByRole as { role, specs } (role.id)}
      <div class="role-section">
        <h3 class="role-header">
          {role.name}
        </h3>
        <div class="spec-grid">
          {#each specs as spec (spec.fullNameSlug)}
            <Button
              variant="unstyled"
              class={[
                'home-page-button',
                `spec-card`,
                { disabled: !selectedBoss, active: selectedSpec === spec.fullNameSlug }
              ]}
              style="--class-accent: {getSpecClassColor(spec)}"
              onclick={() => selectSpec(spec)}
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
            </Button>
          {/each}
        </div>
      </div>
    {/each}
  {/if}

  {#if isSelectionCollapsed}
    {#if rankingsLoading}
      <Spinner loadingText="Loading rankings..." />
    {:else if rankingsError}
      <Alert variant="error">{rankingsError}</Alert>
    {:else if rankings}
      <RankingsList {rankings} edition={selectedEditionSlug} bossIconName={selectedBossIconName} />
    {:else if selectedBoss && selectedSpec && lastRankingsRequestKey}
      <Alert>No rankings found for this selection</Alert>
    {/if}
  {/if}
</div>

<style lang="scss">
  @use '../lib/styles/selection-tokens' as selectionTokens;

  .home-page {
    @include selectionTokens.apply-selection-tokens;

    max-width: 1600px;
    margin: 0 auto;
  }

  .comparison-open .selection-collapsed {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 8px 12px;
    border-radius: 4px;
    margin-bottom: 10px;
  }
  .comparison-open .selection-collapsed > .label {
    display: none;
  }
  .comparison-open .selection-collapsed .value {
    font-size: 12px;
    margin: 0;
  }
  .comparison-open .selection-collapsed .selection-overview {
    flex: 1;
    margin: 0;
  }
  .comparison-open .selection-spec-media {
    width: 40px;
    height: 40px;
  }
  .comparison-open .selection-spec-media .spec-icon {
    width: 38px;
    height: 38px;
  }
  .comparison-open .selection-spec-media .class-icon {
    width: 20px;
    height: 20px;
  }
  .comparison-open :global(.edit-selection.home-page-button) {
    margin: 0;
    padding: 6px 10px;
    font-size: 12px;
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

  :global(.spec-card.home-page-button) {
    width: 100%;
    border: 1px solid
      color-mix(in srgb, var(--class-accent, var(--select-border)) 22%, var(--select-border));
    border-radius: var(--radius-md);
    background: var(--spec-bg);
    color: color-mix(
      in srgb,
      var(--summary-value) 72%,
      var(--class-accent, var(--summary-value)) 28%
    );
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

  :global(.spec-card.home-page-button:hover:not(.disabled)) {
    border-color: color-mix(
      in srgb,
      var(--class-accent, var(--select-hover-border)) 48%,
      var(--select-hover-border)
    );
    background: var(--spec-hover-bg);
    box-shadow: var(--spec-hover-ring);
    transform: translateY(-1px);
  }

  :global(.spec-card.active.home-page-button) {
    border-color: color-mix(
      in srgb,
      var(--class-accent, var(--select-focus-border)) 72%,
      #ffffff 28%
    );
    background: color-mix(
      in srgb,
      var(--class-accent, var(--select-hover-bg)) 12%,
      var(--spec-hover-bg)
    );
    box-shadow:
      0 0 0 1px color-mix(in srgb, var(--class-accent, var(--select-focus-border)) 42%, transparent),
      var(--select-focus-ring);
  }

  :global(.spec-card.disabled.home-page-button) {
    background: linear-gradient(160deg, var(--disabled-bg), var(--disabled-bg-alt));
    border-color: var(--disabled-border);
    color: var(--disabled-text);
    cursor: not-allowed;
    opacity: 0.88;
    box-shadow:
      inset 0 0 0 1px color-mix(in srgb, var(--disabled-border) 75%, #ffffff 25%),
      0 1px 2px rgba(67, 88, 111, 0.12);
  }

  :global(.spec-card.disabled.home-page-button) .spec-icon {
    border-color: var(--disabled-border);
    filter: grayscale(0.78) saturate(0.2) brightness(0.9);
  }

  :global(.spec-card.disabled.home-page-button) .class-icon {
    border-color: var(--disabled-icon-bg);
    background: var(--disabled-icon-bg);
    filter: grayscale(0.78) saturate(0.2) brightness(0.9);
  }

  :global(.spec-card.disabled.home-page-button) .spec-name {
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

  :global(.spec-card.active.home-page-button) .spec-name {
    color: color-mix(in srgb, currentColor 62%, var(--class-accent, currentColor) 38%);
  }

  .selection-collapsed {
    background: transparent;
    border: 0;
    border-bottom: 1px solid var(--summary-border);
    border-radius: 0;
    box-shadow: none;
    padding: 0.35rem 0 0.75rem;
    margin-bottom: var(--selector-block-gap);
    display: grid;
    gap: var(--space-2);
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

  .selection-title {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 0.65rem;
    margin: 0.5rem 0 0.25rem;
    font-size: 1.4rem;
    line-height: 1.3;
    color: var(--summary-value);
  }

  .selection-title .spec-value {
    color: oklch(
      from
        color-mix(
          in srgb,
          var(--class-accent, var(--summary-value)) var(--class-text-share, 55%),
          var(--class-text-shade, #172433)
        )
        min(l, var(--class-text-ceiling, 0.49)) c h
    );
  }

  .versus {
    color: var(--summary-label);
    font-size: 1rem;
    font-weight: 400;
  }

  .selected-spec,
  .selected-boss {
    display: inline-flex;
    align-items: center;
    gap: 0.5rem;
  }

  :global(.edit-selection.home-page-button) {
    justify-self: start;
    margin-top: var(--space-2);
  }

  @media (max-width: 700px) {
    .comparison-open .selection-collapsed {
      position: relative;
      flex-direction: column;
      align-items: stretch;
      gap: 4px;
      padding: 44px 0 10px;
    }
    .comparison-open .selection-collapsed .selection-overview {
      min-width: 0;
    }
    .comparison-open .selection-collapsed .value {
      position: absolute;
      top: 12px;
      left: 0;
      max-width: calc(100% - 135px);
    }
    .selection-overview > div {
      width: 100%;
      min-width: 0;
    }
    .selection-title {
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto minmax(0, 1fr);
      gap: 8px;
      width: 100%;
      font-size: clamp(11px, 3vw, 16px);
    }
    .selection-title .selected-spec,
    .selection-title .selected-boss {
      display: flex;
      flex-direction: column;
      align-items: center;
      min-width: 0;
      gap: 8px;
      text-align: center;
      overflow-wrap: anywhere;
    }
    .selection-title .versus {
      text-align: center;
      font-size: 0.8rem;
    }
    .comparison-open :global(.edit-selection.home-page-button) {
      position: absolute;
      top: 0;
      right: 0;
      min-height: 40px;
    }
    .spec-grid {
      grid-template-columns: 1fr 1fr;
    }
  }

  :global(.theme-dark) :global(.spec-card.disabled.home-page-button) {
    background: linear-gradient(160deg, var(--disabled-bg), var(--disabled-bg-alt));
    border-color: var(--disabled-border);
    color: var(--disabled-text);
    cursor: not-allowed;
    opacity: 1;
    box-shadow: var(--disabled-icon-ring);
  }

  :global(.theme-dark) :global(.spec-card.disabled.home-page-button) .spec-icon {
    border-color: var(--disabled-border);
    filter: grayscale(0.68) saturate(0.28) brightness(0.85);
  }

  :global(.theme-dark) :global(.spec-card.disabled.home-page-button) .class-icon {
    border-color: var(--disabled-icon-bg);
    background: var(--disabled-icon-bg);
    filter: grayscale(0.68) saturate(0.28) brightness(0.85);
  }

  :global(.theme-dark) :global(.spec-card.disabled.home-page-button) .spec-name {
    color: var(--disabled-text);
    font-weight: 500;
  }
</style>
