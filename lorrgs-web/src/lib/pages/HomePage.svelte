<script>
  import { getVersionIconUrl } from '../selectionIcons'
  import {
    getAvailableSpecsForVersion,
    getRaidBossOptions,
    getRaidsForVersion,
    groupRaidsByPhase
  } from '../raidCatalog'

  export let worldData = {}
  export let raidCatalog = { editions: [], instances: {} }
  export let onSelectSpec = () => {}

  let selectedVersion = ''
  let selectedRaid = ''
  let selectedBoss = ''

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

  // Group specs by roleId, then map to role objects
  $: versionSpecs = getAvailableSpecsForVersion(worldData.specs || [], selectedVersion)
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
</script>

<div class="home-page">
  <div class="hero">
    <h2>WoW Rankings Timeline</h2>
    <p>Visualize cooldown usage across top parses for every spec and boss.</p>
  </div>

  <div class="boss-selector">
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
        <option value="">— choose a raid —</option>
        {#each raidPhaseGroups as group (group.key)}
          <optgroup label={group.label}>
            {#each group.raids as raid (raid.slug)}
              <option value={raid.slug}>{raid.name}</option>
            {/each}
          </optgroup>
        {/each}
      </select>
    </div>

    <label>
      <span>Select Boss:</span>
      <select value={selectedBoss} on:change={handleBossChange}>
        <option value="">— choose a boss —</option>
        {#each bosses as boss (boss.slug)}
          <option value={boss.slug} disabled={!boss.mapped}>
            {boss.name}{boss.mapped ? '' : ' (unsupported)'}
          </option>
        {/each}
      </select>
    </label>
  </div>

  {#each specsByRole as { role, specs } (role.id)}
    <div class="role-section">
      <h3 class="role-header" style="color: {role.color}">
        {role.name}
      </h3>
      <div class="spec-grid">
        {#each specs as spec (spec.fullNameSlug)}
          <button
            class="spec-card"
            class:disabled={!selectedBoss}
            on:click={() => selectSpec(spec)}
            title={selectedBoss ? `View ${spec.fullName}` : 'Select a boss first'}
          >
            <span class="spec-name">{spec.fullName}</span>
          </button>
        {/each}
      </div>
    </div>
  {/each}
</div>

<style>
  .home-page {
    max-width: 1000px;
    margin: 0 auto;
  }

  .hero {
    text-align: center;
    margin-bottom: 2rem;
  }

  .hero h2 {
    font-size: 2rem;
    margin: 0 0 0.5rem;
  }

  .hero p {
    color: #aaa;
    margin: 0;
  }

  .boss-selector {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
    gap: 1rem;
    margin-bottom: 2.5rem;
    padding: 1rem 1.25rem;
    background: linear-gradient(180deg, #1e1410 0%, #150d0a 100%);
    border-radius: 0.75rem;
    border: 1px solid #4a3329;
    box-shadow:
      inset 0 1px 0 rgba(255, 214, 170, 0.08),
      0 10px 24px rgba(0, 0, 0, 0.25);
  }

  .selector-block {
    grid-column: 1 / -1;
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

  .boss-selector label {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
    font-size: 1rem;
  }

  .boss-selector select {
    padding: 0.5rem 1rem;
    background: #251913;
    border: 1px solid #5e4032;
    color: #f6e8d8;
    border-radius: 0.5rem;
    font-size: 1rem;
    min-width: 240px;
  }

  .role-section {
    margin-bottom: 2rem;
  }

  .role-header {
    font-size: 1rem;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.08em;
    margin: 0 0 0.75rem;
    padding-bottom: 0.4rem;
    border-bottom: 1px solid #333;
  }

  .spec-grid {
    display: flex;
    flex-wrap: wrap;
    gap: 0.5rem;
  }

  .spec-card {
    padding: 0.45rem 0.9rem;
    background: #1e1e2e;
    border: 1px solid #333;
    border-radius: 0.4rem;
    color: #ddd;
    cursor: pointer;
    font-size: 0.875rem;
    transition:
      background 0.15s,
      border-color 0.15s;
  }

  .spec-card:hover:not(.disabled) {
    background: #2a2a4a;
    border-color: #555;
    color: #fff;
  }

  .spec-card.disabled {
    opacity: 0.4;
    cursor: default;
  }

  .spec-name {
    white-space: nowrap;
  }
</style>
