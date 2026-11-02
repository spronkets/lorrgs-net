<script lang="ts">
  import Alert from '../lib/components/Alert.svelte';
  import Spinner from '../lib/components/Spinner.svelte';
  import { onDestroy, onMount, tick } from 'svelte';
  import * as API from '../lib/api';
  import { manualRankingReport, parseReportCodes, readReportOverview } from '../lib/manualReports';
  import type { RankingReport } from '../lib/models/RankingReport';
  import type { SpecRankingData } from '../lib/models/SpecRankingData';
  import type { WorldDataSnapshot } from '../lib/models/WorldDataSnapshot';
  import type { WowClass } from '../lib/models/WowClass';
  import type { WowRole } from '../lib/models/WowRole';
  import type { WowSpec } from '../lib/models/WowSpec';
  import type { RankingFilters } from '../lib/models/rankings/RankingFilters';
  import type { RankingSort } from '../lib/models/rankings/RankingSort';
  import EditionSelector from '../lib/components/EditionSelector.svelte';
  import PageHeader from '../lib/components/PageHeader.svelte';
  import RaidBossSelectors from '../lib/components/RaidBossSelectors.svelte';
  import RankingsList from '../lib/components/RankingsList.svelte';
  import SpecSelector from '../lib/components/SpecSelector.svelte';
  import { getVersionIconUrl } from '../lib/selectionIcons';
  import {
    DISABLED_EDITION_OPTIONS,
    EDITION_OPTIONS,
    getApiEditionSlug,
    getAvailableSpecsForVersion,
    groupRaidsByPhase,
    getRaidBossOptions,
    getRaidsForEdition
  } from '../lib/raidCatalog';
  import type { RaidCatalog } from '../lib/models/RaidCatalog';
  import { toKillTimeInput } from '../lib/rankingUtils';

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
  let lastRankingsRequestKey = '';
  let hasHydratedQuery = false;
  let linkedReport: RankingReport | null = null;
  let linkedEncounterKey = '';
  let linkedReportLoading = false;
  let linkedReportError = '';
  let rankingFilters: RankingFilters = {
    region: '',
    minDuration: '',
    maxDuration: '',
    fromDate: '',
    toDate: ''
  };
  let rankingSort: RankingSort = 'rank';
  let selectedDifficultyId = 3;
  const linkedController = new AbortController();
  onDestroy(() => linkedController.abort());

  async function loadLinkedReport() {
    const params = new URLSearchParams(window.location.search);
    const code = params.get('reportCode');
    const fightIdValue = params.get('fightId');
    if (!code && !fightIdValue) return;
    linkedReportLoading = true;
    linkedEncounterKey = `${selectedVersion}|${selectedRaid}|${selectedBoss}`;
    try {
      const fightId = Number(fightIdValue);
      if (!code || !Number.isSafeInteger(fightId) || fightId <= 0)
        throw new Error('A linked boss reference requires a report code and a positive fight ID.');
      const reportCode = parseReportCodes(code)[0];
      const overview = readReportOverview(await API.lookupReport(reportCode, linkedController.signal));
      if (linkedController.signal.aborted) return;
      const fight = overview.fights.find((entry) => entry.id === fightId);
      if (!fight) throw new Error('The linked boss fight was not found in this report.');
      const raid = raids.find((entry) =>
        entry.isAvailable !== false &&
        getRaidBossOptions(entry).some((boss) => boss.id === fight.encounterID)
      );
      const boss = getRaidBossOptions(raid).find((entry) => entry.id === fight.encounterID);
      if (!raid || !boss)
        throw new Error('The linked encounter is not available in the selected edition.');
      selectedRaid = raid.slug;
      selectedBoss = boss.slug;
      selectedSpec = params.get('spec') || selectedSpec;
      linkedReport = manualRankingReport(overview, fight);
      linkedEncounterKey = `${selectedVersion}|${selectedRaid}|${selectedBoss}`;
    } catch (failure) {
      if (linkedController.signal.aborted) return;
      linkedReportError = failure instanceof Error ? failure.message : 'Unable to load linked boss reference.';
      console.error('Linked boss reference failed:', failure);
    } finally {
      if (!linkedController.signal.aborted) linkedReportLoading = false;
    }
  }

  const versionOrder = EDITION_OPTIONS;
  const versions = versionOrder;
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
  const defaultDifficultyIdFor = (version: string) => (version === 'Retail' ? 5 : 3);

  $: raids = getRaidsForEdition(raidCatalog, selectedVersion);
  $: if ((!selectedVersion || !versions.includes(selectedVersion)) && versions.length) {
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
  $: difficultyOptions = difficultyOptionsFor(selectedVersion);
  $: if (!difficultyOptions.some((option) => option.value === selectedDifficultyId)) {
    selectedDifficultyId = defaultDifficultyIdFor(selectedVersion);
  }

  $: if (
    selectedRaid &&
    !raids.some((raid) => raid.slug === selectedRaid && raid.isAvailable !== false)
  ) {
    selectedRaid = '';
  }

  $: if (selectedBoss && !bosses.some((boss) => boss.slug === selectedBoss)) {
    selectedBoss = '';
  }

  $: versionSpecs = getAvailableSpecsForVersion(worldData.specs || [], selectedVersion);
  $: classesById = new Map((worldData.classes || []).map((cls) => [Number(cls.id), cls]));
  $: rolesById = new Map((worldData.roles || []).map((role) => [Number(role.id), role]));
  $: selectedSpecMeta = versionSpecs.find((spec) => spec.fullNameSlug === selectedSpec);
  $: specOptions = versionSpecs.map((spec) => ({
    slug: spec.fullNameSlug,
    fullName: spec.fullName,
    roleName: getRoleGroupName(spec),
    classSlug: getSpecClassSlug(spec),
    specSlug: spec.nameSlug
  }));
  $: selectedClassMeta = selectedSpecMeta ? getSpecClass(selectedSpecMeta) : null;
  $: selectedBossMeta = bosses.find((boss) => boss.slug === selectedBoss);
  $: selectedWorldBoss = (worldData.bosses || []).find((boss) => boss.nameSlug === selectedBoss);
  $: selectedEditionSlug = getApiEditionSlug(selectedVersion);
  $: selectedMetric = selectedSpecMeta ? resolveMetricForSpec(selectedSpecMeta) : 'dps';
  $: selectedDifficulty =
    difficultyOptions.find((option) => option.value === selectedDifficultyId)?.label || 'Normal';
  $: selectedZoneId = selectedRaidOption?.zoneId || selectedWorldBoss?.raidId || 0;
  $: selectedEncounterId = selectedBossMeta?.id || selectedWorldBoss?.id || 0;
  $: if (selectedSpec && !versionSpecs.some((spec) => spec.fullNameSlug === selectedSpec)) {
    selectedSpec = '';
  }

  function selectVersion(version: string) {
    if (selectedVersion === version || DISABLED_EDITION_OPTIONS.includes(version)) return;
    selectedVersion = version;
    selectedRaid = '';
    selectedBoss = '';
    selectedSpec = '';
    oneditionchange?.(version);
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

  function getRoleGroupName(spec: WowSpec): string {
    const role = rolesById.get(Number(spec.roleId));
    const code = String(role?.code || '').toLowerCase();
    const name = String(role?.name || '').toLowerCase();
    if (code === 'tank' || name.includes('tank')) return 'Tank';
    if (code === 'heal' || name.includes('heal')) return 'Healer';
    if (code === 'dps' || code === 'mdps' || code === 'rdps' || name.includes('dps')) return 'DPS';
    return 'Other';
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
    const queryDifficulty = Number(params.get('difficultyId') ?? defaultDifficultyIdFor(selectedVersion));
    if (Number.isInteger(queryDifficulty)) {
      selectedDifficultyId = queryDifficulty;
    }

    rankingFilters = {
      region: params.get('region') ?? '',
      minDuration: toKillTimeInput(params.get('killtime_min') ?? ''),
      maxDuration: toKillTimeInput(params.get('killtime_max') ?? ''),
      fromDate: params.get('fromDate') ?? '',
      toDate: params.get('toDate') ?? ''
    };
    const querySort = (params.get('sort') ?? 'rank') as RankingSort;
    rankingSort = ['rank', 'performance', 'duration', 'date'].includes(querySort)
      ? querySort
      : 'rank';

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
    if (difficultyOptions.length > 1) {
      url.searchParams.set('difficultyId', String(selectedDifficultyId));
    } else {
      url.searchParams.delete('difficultyId');
    }

    if (rankingFilters.region) url.searchParams.set('region', rankingFilters.region);
    else url.searchParams.delete('region');
    if (rankingFilters.minDuration) url.searchParams.set('killtime_min', rankingFilters.minDuration);
    else url.searchParams.delete('killtime_min');
    if (rankingFilters.maxDuration) url.searchParams.set('killtime_max', rankingFilters.maxDuration);
    else url.searchParams.delete('killtime_max');
    if (rankingFilters.fromDate) url.searchParams.set('fromDate', rankingFilters.fromDate);
    else url.searchParams.delete('fromDate');
    if (rankingFilters.toDate) url.searchParams.set('toDate', rankingFilters.toDate);
    else url.searchParams.delete('toDate');
    if (rankingSort !== 'rank') url.searchParams.set('sort', rankingSort);
    else url.searchParams.delete('sort');

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
          serverRegion: rankingFilters.region || undefined,
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
    loadSelectionsFromQuery();
    const params = new URLSearchParams(window.location.search);
    const hasLinkedReport = params.has('reportCode') || params.has('fightId');
    if (!hasLinkedReport) hasHydratedQuery = true;

    // Wait one microtask so derived selections settle before loading.
    await tick();
    if (hasLinkedReport) {
      await loadLinkedReport();
      if (linkedController.signal.aborted) return;
      await tick();
      hasHydratedQuery = true;
    }
  });

  $: if (hasHydratedQuery && selectedBoss && selectedSpec && !linkedReportError) {
    void selectedDifficultyId;
    void loadRankings();
  }

  $: if (
    hasHydratedQuery && (linkedReport || linkedReportError) &&
    linkedEncounterKey !== `${selectedVersion}|${selectedRaid}|${selectedBoss}`
  ) {
    linkedReport = null;
    linkedReportError = '';
    const url = new URL(window.location.href);
    for (const key of ['reportCode', 'fightId', 'sourceId', 'playerName', 'autoAnalyze'])
      url.searchParams.delete(key);
    window.history.replaceState({}, '', `${url.pathname}${url.search}${url.hash}`);
  }

  $: if (typeof window !== 'undefined' && hasHydratedQuery) {
    void selectedVersion;
    void selectedRaid;
    void selectedBoss;
    void selectedSpec;
    void selectedDifficultyId;
    syncSelectionsToQuery();
  }
</script>

<div class="home-page">
  <PageHeader
    title="Spec Rankings"
    description="Compare cooldown usage across ranked players of a chosen specialization."
  />
  {#if linkedReportLoading}
    <div role="status"><Spinner inline />Loading linked boss reference...</div>
  {/if}
  {#if linkedReportError}
    <Alert variant="error">{linkedReportError}</Alert>
  {/if}
  <section class="edition-filter" aria-label="Edition selection">
    <h3 class="edition-label">Edition</h3>
    <EditionSelector
      versions={versions}
      {selectedVersion}
      disabledVersions={DISABLED_EDITION_OPTIONS}
      {getVersionIconUrl}
      onselect={selectVersion}
    />
  </section>
  <section class="spec-filters" aria-label="Specialization selection">
    <SpecSelector
      label="Specialization"
      placeholder="— choose a specialization —"
      bind:value={selectedSpec}
      disabled={!specOptions.length}
      options={specOptions}
    />
  </section>
  {#if selectedSpecMeta || selectedBoss}
    <p class="selection-separator">vs.</p>
  {/if}
  <section class="encounter-and-difficulty" aria-label="Encounter and difficulty selection">
    <RaidBossSelectors
      raidPhaseGroups={groupRaidsByPhase(raids)}
      bosses={bossOptions}
      bind:selectedRaid
      bind:selectedBoss
      raidPlaceholder="— choose a raid —"
      bossPlaceholder="— choose a boss —"
    />
    {#if difficultyOptions.length > 1}
      <label class="difficulty-control"
        >Difficulty
        <select bind:value={selectedDifficultyId} aria-label="Difficulty">
          {#each difficultyOptions as option (option.value)}
            <option value={option.value}>{option.label}</option>
          {/each}
        </select>
      </label>
    {/if}
  </section>

  {#if rankingsLoading}
    <Spinner loadingText="Loading rankings..." />
  {:else if rankingsError}
    <Alert variant="error">{rankingsError}</Alert>
  {:else if rankings}
    <RankingsList
      {rankings}
      {linkedReport}
      edition={selectedEditionSlug}
      bossIconName={selectedBossIconName}
      initialFilters={rankingFilters}
      initialSort={rankingSort}
    />
  {:else if selectedBoss && selectedSpec && lastRankingsRequestKey}
    <Alert>No rankings found for this selection</Alert>
  {/if}
</div>

<style lang="scss">
  @use '../lib/styles/selection-tokens' as selectionTokens;

  .home-page {
    @include selectionTokens.apply-selection-tokens;

    max-width: 1600px;
    margin: 0 auto;
  }

  .edition-filter {
    display: grid;
    gap: var(--space-3);
    margin-bottom: var(--space-5);
  }

  .edition-label {
    @include selectionTokens.selection-label;

    margin: 0;
  }

  .spec-filters {
    display: grid;
    grid-template-columns: minmax(0, 1fr);
    gap: var(--space-5, 0.8rem);
    margin-bottom: var(--space-4);
  }

  .selection-separator {
    margin: 0 0 var(--space-4);
    font-size: 1rem;
    line-height: 1.25;
    color: var(--summary-label);
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.08em;
  }

  .encounter-and-difficulty {
    display: grid;
    gap: var(--space-5, 0.8rem);
    margin-bottom: var(--space-6);
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

  @media (max-width: 700px) {
    .selection-separator {
      font-size: 0.95rem;
    }
  }
</style>
