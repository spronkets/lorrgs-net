<script lang="ts">
  import { onMount } from 'svelte'
  import * as API from '../api'
  import * as Cache from '../cache'
  import { getVersionIconUrl } from '../selectionIcons'
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

  const difficulties = ['Normal', 'Heroic', 'Mythic']
  const metrics = ['dps', 'hps', 'wdps']
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

  $: availableSpecs = getAvailableSpecsForVersion(worldData.specs || [], selectedVersion)

  $: if (selectedSpec && !availableSpecs.some((spec) => spec.fullNameSlug === selectedSpec)) {
    selectedSpec = ''
  }

  function onVersionChange() {
    selectedRaid = ''
    selectedBoss = ''
  }

  function onRaidChange() {
    selectedBoss = ''
  }

  function selectVersion(version) {
    if (selectedVersion === version) return
    selectedVersion = version
    onVersionChange()
  }

  function handleRaidChange(event) {
    const normalized = event.currentTarget.value
    if (selectedRaid === normalized) return
    selectedRaid = normalized
    onRaidChange()
  }

  function handleBossChange(event) {
    selectedBoss = event.currentTarget.value
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
    resolveSelectionFromBoss(initialBoss)
  })

  $: if (selectedBoss || selectedSpec || selectedDifficulty || selectedMetric) {
    loadRankings()
  }
</script>

<div class="rankings-page">
  <h2>Spec Rankings</h2>

  <div class="selector-cards">
    <div class="selector-block">
      <h3>Choose Version</h3>
      <div class="icon-strip">
        {#each versions as version (version)}
          <button
            class="icon-card"
            class:version-card={true}
            class:active={selectedVersion === version}
            on:click={() => selectVersion(version)}
            title={version}
          >
            <img src={getVersionIconUrl(version)} alt={version} class="selector-icon banner" />
            <span>{version}</span>
          </button>
        {/each}
      </div>
    </div>

    <div class="selector-block">
      <h3>Choose Raid</h3>
      <select value={selectedRaid} on:change={handleRaidChange}>
        <option value="">Select a raid...</option>
        {#each raidPhaseGroups as group (group.key)}
          <optgroup label={group.label}>
            {#each group.raids as raid (raid.slug)}
              <option value={raid.slug}>{raid.name}</option>
            {/each}
          </optgroup>
        {/each}
      </select>
    </div>
  </div>

  <div class="controls">
    <div class="control-group">
      <label>
        <span>Boss:</span>
        <select value={selectedBoss} on:change={handleBossChange}>
          <option value="">Select a boss...</option>
          {#each bosses as boss (boss.slug)}
            <option value={boss.slug} disabled={!boss.mapped}>
              {boss.name}{boss.mapped ? '' : ' (unsupported)'}
            </option>
          {/each}
        </select>
      </label>
    </div>

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

<style>
  .rankings-page {
    animation: fadeIn 0.3s ease-in-out;
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
    font-size: 1.8rem;
  }

  .selector-cards {
    display: grid;
    gap: 1rem;
    margin-bottom: 1rem;
  }

  .selector-block {
    background: linear-gradient(180deg, #22160f 0%, #180f0b 100%);
    border: 1px solid #4a3329;
    border-radius: 0.85rem;
    padding: 0.9rem;
  }

  .selector-block h3 {
    margin: 0 0 0.65rem;
    font-size: 0.95rem;
    font-weight: 700;
    color: #d9b48d;
    text-transform: uppercase;
    letter-spacing: 0.04em;
  }

  .icon-strip {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(132px, 1fr));
    gap: 0.55rem;
  }

  .icon-card {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 0.35rem;
    background: #241913;
    color: #f6e8d8;
    border: 1px solid #5e4032;
    border-radius: 0.55rem;
    padding: 0.5rem;
    cursor: pointer;
    transition:
      border-color 0.15s,
      background 0.15s;
  }

  .icon-card:hover {
    border-color: #9f6c54;
    background: #2b1d16;
  }

  .icon-card.active {
    border-color: #f1b37d;
    box-shadow: 0 0 0 1px rgba(241, 179, 125, 0.35) inset;
  }

  .selector-icon {
    width: 42px;
    height: 42px;
    object-fit: cover;
    border-radius: 0.4rem;
    border: 1px solid #6a4736;
    background: #120d0a;
  }

  .icon-card span {
    font-size: 0.78rem;
    line-height: 1.2;
    text-align: center;
  }

  .icon-card.version-card {
    padding: 0.4rem;
    align-items: stretch;
  }

  .selector-icon.banner {
    width: 100%;
    height: auto;
    aspect-ratio: 4 / 3;
    border-radius: 0.45rem;
  }

  .icon-card.version-card span {
    font-size: 0.76rem;
    margin-top: 0.1rem;
  }

  .controls {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
    gap: 1rem;
    margin-bottom: 2rem;
    padding: 1.25rem;
    background: linear-gradient(180deg, #1e1410 0%, #150d0a 100%);
    border-radius: 0.85rem;
    border: 1px solid #4a3329;
    box-shadow:
      inset 0 1px 0 rgba(255, 214, 170, 0.08),
      0 10px 24px rgba(0, 0, 0, 0.25);
  }

  .control-group label {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
  }

  .control-group span {
    font-weight: 600;
    font-size: 0.9rem;
    color: #d9b48d;
    letter-spacing: 0.01em;
  }

  .control-group select {
    background: #251913;
    border: 1px solid #5e4032;
    color: #f6e8d8;
    padding: 0.5rem;
    border-radius: 0.25rem;
    font-size: 0.95rem;
    cursor: pointer;
  }

  .control-group select:hover {
    border-color: #8c5f4a;
  }

  .control-group select:focus {
    outline: none;
    border-color: #d59b72;
    background: #2d1f18;
  }

  .control-group select option {
    background: #231811;
    color: #f2ddc8;
    padding: 0.5rem;
  }

  .control-group select option:checked {
    background: #55392b;
    color: #fff4e8;
  }

  .error {
    background: #4a2020;
    border: 1px solid #8b3a3a;
    color: #ff9999;
    padding: 1rem;

    border-radius: 0.5rem;
    border: 1px solid #333;
  }
</style>
