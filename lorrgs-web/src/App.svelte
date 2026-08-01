<script lang="ts">
  import { onMount } from 'svelte';
  import * as API from './lib/api';
  import HomePage from './lib/pages/HomePage.svelte';
  import CompRankingsPage from './lib/pages/CompRankingsPage.svelte';
  import WorldDataPage from './lib/pages/WorldDataPage.svelte';
  import RotationAnalyzerPage from './lib/pages/RotationAnalyzerPage.svelte';

  let currentPage = 'home';
  let worldData = {
    classes: [],
    specs: [],
    roles: [],
    bosses: [],
    zones: [],
    spells: [],
    trinkets: [],
    seasons: []
  };
  let raidCatalog = {
    editions: [],
    instances: {}
  };
  let loading = true;
  let error = '';
  let theme = 'light';
  let hasHydratedQuery = false;

  const THEME_STORAGE_KEY = 'lorrgs-theme';
  const PAGE_QUERY_KEY = 'page';
  const APP_PAGES = new Set(['home', 'comp', 'worlddata', 'rotation']);

  function applyTheme(nextTheme) {
    theme = nextTheme === 'dark' ? 'dark' : 'light';
  }

  function toggleTheme() {
    applyTheme(theme === 'dark' ? 'light' : 'dark');
    localStorage.setItem(THEME_STORAGE_KEY, theme);
  }

  function initializeTheme() {
    const storedTheme = localStorage.getItem(THEME_STORAGE_KEY);
    if (storedTheme === 'light' || storedTheme === 'dark') {
      applyTheme(storedTheme);
      return;
    }

    const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    applyTheme(prefersDark ? 'dark' : 'light');
  }

  function initializeSelectionsFromQuery() {
    const params = new URLSearchParams(window.location.search);
    const requestedPage = params.get(PAGE_QUERY_KEY);

    if (requestedPage && APP_PAGES.has(requestedPage)) {
      currentPage = requestedPage;
    }
  }

  function syncPageQueryParam() {
    const url = new URL(window.location.href);

    if (currentPage === 'home') {
      url.searchParams.delete(PAGE_QUERY_KEY);
    } else {
      url.searchParams.set(PAGE_QUERY_KEY, currentPage);
    }

    const nextUrl = `${url.pathname}${url.search}${url.hash}`;
    window.history.replaceState({}, '', nextUrl);
  }

  async function loadWorldData() {
    try {
      const apiHealthy = await API.waitForHealthReady();
      if (!apiHealthy) {
        throw new Error('API is not ready yet (health endpoint did not return healthy).');
      }

      const initialRaidCatalog = await API.getRaidCatalog().catch(() => ({
        editions: [],
        instances: {}
      }));

      const [classes, specs, roles, bosses, zones, spells, trinkets, seasons] = await Promise.all([
        API.getClasses().catch(() => ({})),
        API.getSpecs().catch(() => ({ specs: [] })),
        API.getRoles().catch(() => ({ roles: [] })),
        API.getBosses().catch(() => ({ bosses: [] })),
        API.getZones().catch(() => ({ zones: [] })),
        API.getSpells().catch(() => ({ spells: [] })),
        API.getTrinkets().catch(() => ({ trinkets: [] })),
        API.getSeasons().catch(() => ({ seasons: [] }))
      ]);

      const data = {
        classes: API.normalizeApiCollection(classes, 'classes'),
        specs: API.normalizeApiCollection(specs, 'specs'),
        roles: API.normalizeApiCollection(roles, 'roles'),
        bosses: API.normalizeApiCollection(bosses, 'bosses'),
        zones: API.normalizeApiCollection(zones, 'zones'),
        spells: API.normalizeApiCollection(spells, 'spells'),
        trinkets: API.normalizeApiCollection(trinkets, 'trinkets'),
        seasons: API.normalizeApiCollection(seasons, 'seasons')
      };

      worldData = data;
      raidCatalog = initialRaidCatalog;
      error = '';
    } catch (err) {
      error = err instanceof Error ? err.message : 'Unknown error';
      console.error('Failed to load world data:', err);
    } finally {
      loading = false;
    }
  }

  onMount(() => {
    initializeTheme();
    initializeSelectionsFromQuery();
    hasHydratedQuery = true;
    loadWorldData();
  });

  $: if (typeof window !== 'undefined' && hasHydratedQuery) {
    void currentPage;
    syncPageQueryParam();
  }
</script>

<div class="app" class:theme-dark={theme === 'dark'} class:theme-light={theme === 'light'}>
  <header class="app-header">
    <div class="header-content">
      <button
        class="brand-toggle"
        type="button"
        on:click={toggleTheme}
        title={theme === 'dark' ? 'Switch to Light mode' : 'Switch to Dark mode'}
        aria-label={theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'}
      >
        LorrgsNET
      </button>
      <p>
        Warcraft Logs rankings and performance analyzer based on
        <a href="https://lorrgs.io/" target="_blank" rel="noreferrer">lorrgs.io</a>.
      </p>
    </div>
    <nav class="app-nav">
      <button class:active={currentPage === 'home'} on:click={() => (currentPage = 'home')}>
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
        <HomePage {worldData} {raidCatalog} />
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
    color: #132032;
  }

  :global(html) {
    scroll-behavior: smooth;
  }

  :global(*) {
    box-sizing: border-box;
  }

  .app {
    --app-bg:
      radial-gradient(circle at 8% 10%, rgba(248, 232, 197, 0.24), transparent 42%),
      radial-gradient(circle at 88% 0%, rgba(72, 173, 167, 0.22), transparent 34%),
      linear-gradient(160deg, #eef3f8 0%, #dde8f2 47%, #d3e2ed 100%);
    --header-bg: rgba(255, 255, 255, 0.82);
    --header-line: rgba(43, 78, 110, 0.18);
    --title-color: #102334;
    --subtitle-color: #3e607f;
    --eyebrow-color: #317f7a;
    --nav-bg: rgba(245, 249, 252, 0.78);
    --nav-line: #b6ccdc;
    --nav-text: #224360;
    --nav-hover-bg: #ffffff;
    --nav-hover-line: #6b99b9;
    --main-text: #132032;
    --loading-color: #4b6d88;

    display: flex;
    flex-direction: column;
    min-height: 100vh;
    color: var(--main-text);
    background: var(--app-bg);
  }

  .app.theme-dark {
    --app-bg:
      radial-gradient(circle at 10% 8%, rgba(96, 72, 34, 0.24), transparent 40%),
      radial-gradient(circle at 90% 0%, rgba(27, 114, 118, 0.24), transparent 35%),
      linear-gradient(165deg, #08121f 0%, #0e1a2d 45%, #122439 100%);
    --header-bg: rgba(12, 24, 38, 0.82);
    --header-line: rgba(120, 161, 194, 0.24);
    --title-color: #e7f5ff;
    --subtitle-color: #9fc2de;
    --eyebrow-color: #6ec8c2;
    --nav-bg: rgba(22, 39, 57, 0.88);
    --nav-line: #456684;
    --nav-text: #c4dff5;
    --nav-hover-bg: #18334d;
    --nav-hover-line: #76a0c5;
    --main-text: #dceeff;
    --loading-color: #9fc2de;
  }

  .app-header {
    background: var(--header-bg);
    border-bottom: 1px solid var(--header-line);
    backdrop-filter: blur(8px);
    padding: 1.5rem 2rem 1.2rem;
    position: sticky;
    top: 0;
    z-index: 10;
  }

  .header-content {
    max-width: 1400px;
    margin: 0 auto;
    margin-bottom: 1rem;
  }

  .brand-toggle {
    margin: 0;
    padding: 0;
    border: none;
    background: transparent;
    font-size: clamp(1.7rem, 2.5vw, 2.35rem);
    font-weight: 700;
    color: var(--title-color);
    font-family: inherit;
    cursor: pointer;
    transition:
      color 0.2s,
      transform 0.15s;
  }

  .brand-toggle:hover {
    color: #2a8e8f;
  }

  .brand-toggle:focus-visible {
    outline: 2px solid #2a8e8f;
    outline-offset: 3px;
    border-radius: 0.35rem;
  }

  .app.theme-dark .brand-toggle:hover {
    color: #8bddd6;
  }

  .app.theme-dark .brand-toggle:focus-visible {
    outline-color: #8bddd6;
  }

  .header-content p {
    margin: 0.5rem 0 0 0;
    color: var(--subtitle-color);
    font-size: 0.95rem;
  }

  .header-content p a {
    color: var(--title-color);
    text-decoration: none;
    font-weight: 600;
    transition: color 0.2s;
  }

  .header-content p a:hover {
    color: #2a8e8f;
  }

  .header-content p a:focus-visible {
    outline: 2px solid #2a8e8f;
    outline-offset: 2px;
    border-radius: 0.2rem;
  }

  .app.theme-dark .header-content p a:hover {
    color: #8bddd6;
  }

  .app.theme-dark .header-content p a:focus-visible {
    outline-color: #8bddd6;
  }

  .app-nav {
    max-width: 1400px;
    margin: 0 auto;
    display: flex;
    flex-wrap: wrap;
    gap: 0.65rem;
  }

  .app-nav button {
    background: var(--nav-bg);
    border: 1px solid var(--nav-line);
    color: var(--nav-text);
    padding: 0.48rem 0.92rem;
    border-radius: 999px;
    cursor: pointer;
    font-size: 0.88rem;
    font-weight: 600;
    transition: all 0.2s;
  }

  .app-nav button:hover {
    border-color: var(--nav-hover-line);
    background: var(--nav-hover-bg);
    transform: translateY(-1px);
  }

  .app-nav button.active {
    background: linear-gradient(135deg, #1c6f7e, #2a8e8f);
    border-color: #1a6a77;
    color: #effaf9;
    box-shadow: 0 8px 18px rgba(26, 106, 119, 0.23);
  }

  .app-main {
    flex: 1;
    max-width: 1400px;
    margin: 0 auto;
    width: 100%;
    padding: 1.5rem 2rem 2.5rem;
  }

  .error-banner {
    background: #fef2f2;
    border: 1px solid #f3b4b4;
    color: #8a2929;
    padding: 1rem;
    border-radius: 0.75rem;
    margin-bottom: 1.25rem;
    box-shadow: 0 7px 20px rgba(153, 48, 48, 0.08);
  }

  .loading {
    text-align: center;
    padding: 3rem 1rem;
    color: var(--loading-color);
    font-weight: 600;
  }

  @media (max-width: 900px) {
    .app-header {
      padding: 1.25rem 1rem 1rem;
    }

    .app-main {
      padding: 1.1rem 1rem 2rem;
    }
  }
</style>
