<script>
  import { onMount } from 'svelte'
  import * as API from './lib/api'
  import * as Cache from './lib/cache'
  import HomePage from './lib/pages/HomePage.svelte'
  import RankingsPage from './lib/pages/RankingsPage.svelte'
  import CompRankingsPage from './lib/pages/CompRankingsPage.svelte'
  import WorldDataPage from './lib/pages/WorldDataPage.svelte'
  import RotationAnalyzerPage from './lib/pages/RotationAnalyzerPage.svelte'

  let currentPage = 'home'
  let preselectedSpec = ''
  let preselectedBoss = ''
  let worldData = {
    classes: [],
    specs: [],
    roles: [],
    bosses: [],
    zones: [],
    spells: [],
    trinkets: [],
    seasons: []
  }
  let raidCatalog = {
    editions: [],
    instances: {}
  }
  let loading = true
  let error = ''

  async function loadWorldData() {
    try {
      const apiHealthy = await API.waitForHealthReady()
      if (!apiHealthy) {
        throw new Error('API is not ready yet (health endpoint did not return healthy).')
      }

      // Try loading from cache first
      const cacheKey = 'worldData-v3'
      let data = Cache.getWorldDataCache(cacheKey)
      const raidCatalogResponse = await API.getRaidCatalog().catch(() => ({
        editions: [],
        instances: {}
      }))

      const hasValidWorldData =
        data &&
        Array.isArray(data.bosses) &&
        data.bosses.length > 0 &&
        Array.isArray(data.specs) &&
        data.specs.length > 0

      if (!hasValidWorldData) {
        data = null
      }

      if (!data) {
        const [
          classes,
          specs,
          roles,
          bosses,
          zones,
          spells,
          trinkets,
          seasons,
          raidCatalogResponse
        ] = await Promise.all([
          API.getClasses().catch(() => ({})),
          API.getSpecs().catch(() => ({ specs: [] })),
          API.getRoles().catch(() => ({ roles: [] })),
          API.getBosses().catch(() => ({ bosses: [] })),
          API.getZones().catch(() => ({ zones: [] })),
          API.getSpells().catch(() => ({ spells: [] })),
          API.getTrinkets().catch(() => ({ trinkets: [] })),
          API.getSeasons().catch(() => ({ seasons: [] })),
          Promise.resolve(raidCatalogResponse)
        ])

        data = {
          classes: API.normalizeApiCollection(classes, 'classes'),
          specs: API.normalizeApiCollection(specs, 'specs'),
          roles: API.normalizeApiCollection(roles, 'roles'),
          bosses: API.normalizeApiCollection(bosses, 'bosses'),
          zones: API.normalizeApiCollection(zones, 'zones'),
          spells: API.normalizeApiCollection(spells, 'spells'),
          trinkets: API.normalizeApiCollection(trinkets, 'trinkets'),
          seasons: API.normalizeApiCollection(seasons, 'seasons')
        }
        Cache.cacheWorldData(cacheKey, data)
      }

      worldData = data
      raidCatalog = raidCatalogResponse
      error = ''
    } catch (err) {
      error = err instanceof Error ? err.message : 'Unknown error'
      console.error('Failed to load world data:', err)
    } finally {
      loading = false
    }
  }

  /**
   * @param {string} specSlug
   * @param {string} bossSlug
   */
  function goToRankings(specSlug, bossSlug) {
    preselectedSpec = specSlug
    preselectedBoss = bossSlug
    currentPage = 'rankings'
  }

  onMount(loadWorldData)
</script>

<div class="app">
  <header class="app-header">
    <div class="header-content">
      <h1>Lorrgs</h1>
      <p>WarcraftLogs Rankings Dashboard</p>
    </div>
    <nav class="app-nav">
      <button class:active={currentPage === 'home'} on:click={() => (currentPage = 'home')}>
        Home
      </button>
      <button class:active={currentPage === 'rankings'} on:click={() => (currentPage = 'rankings')}>
        Spec Rankings
      </button>
      <button class:active={currentPage === 'comp'} on:click={() => (currentPage = 'comp')}>
        Comp Rankings
      </button>
      <button
        class:active={currentPage === 'worlddata'}
        on:click={() => (currentPage = 'worlddata')}
      >
        World Data
      </button>
      <button class:active={currentPage === 'rotation'} on:click={() => (currentPage = 'rotation')}>
        Rotation Analyzer
      </button>
    </nav>
  </header>

  <main class="app-main">
    {#if error}
      <div class="error-banner">
        <p><strong>Error:</strong> {error}</p>
      </div>
    {/if}

    {#if loading}
      <div class="loading">Loading...</div>
    {:else}
      {#if currentPage === 'home'}
        <HomePage {worldData} {raidCatalog} onSelectSpec={goToRankings} />
      {:else if currentPage === 'rankings'}
        <RankingsPage
          {worldData}
          {raidCatalog}
          initialSpec={preselectedSpec}
          initialBoss={preselectedBoss}
        />
      {:else if currentPage === 'comp'}
        <CompRankingsPage {worldData} />
      {:else if currentPage === 'worlddata'}
        <WorldDataPage {worldData} />
      {:else if currentPage === 'rotation'}
        <RotationAnalyzerPage />
      {/if}
    {/if}
  </main>
</div>

<style>
  :global(body) {
    margin: 0;
    padding: 0;
    font-family:
      -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
    background: #0f0f0f;
    color: #e0e0e0;
  }

  :global(html) {
    scroll-behavior: smooth;
  }

  .app {
    display: flex;
    flex-direction: column;
    min-height: 100vh;
  }

  .app-header {
    background: #1a1a1a;
    border-bottom: 1px solid #333;
    padding: 2rem;
  }

  .header-content {
    max-width: 1400px;
    margin: 0 auto;
    margin-bottom: 1.5rem;
  }

  .header-content h1 {
    margin: 0;
    font-size: 2.5rem;
    font-weight: 700;
  }

  .header-content p {
    margin: 0.5rem 0 0 0;
    color: #999;
    font-size: 0.95rem;
  }

  .app-nav {
    max-width: 1400px;
    margin: 0 auto;
    display: flex;
    gap: 1rem;
  }

  .app-nav button {
    background: transparent;
    border: 1px solid #444;
    color: #ccc;
    padding: 0.5rem 1rem;
    border-radius: 0.25rem;
    cursor: pointer;
    font-size: 0.95rem;
    transition: all 0.2s;
  }

  .app-nav button:hover {
    border-color: #666;
    background: #222;
  }

  .app-nav button.active {
    background: #333;
    border-color: #666;
    color: #fff;
  }

  .app-main {
    flex: 1;
    max-width: 1400px;
    margin: 0 auto;
    width: 100%;
    padding: 2rem;
  }

  .error-banner {
    background: #4a2020;
    border: 1px solid #8b3a3a;
    color: #ff9999;
    padding: 1rem;
    border-radius: 0.5rem;
    margin-bottom: 2rem;
  }

  .loading {
    text-align: center;
    padding: 3rem;
    color: #999;
  }
</style>
