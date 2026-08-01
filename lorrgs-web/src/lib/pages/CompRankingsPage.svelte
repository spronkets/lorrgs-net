<script>
  import * as API from '../api'
  import * as Cache from '../cache'
  import { getWowheadIconUrl } from '../wowheadIcons'

  export let worldData = {}

  let selectedBoss = ''
  let rankings = null
  let loading = false
  let error = ''

  $: boss = (worldData.bosses || []).find((b) => b.nameSlug === selectedBoss)

  async function loadComp() {
    if (!selectedBoss) return
    loading = true
    error = ''

    const cacheKey = `comp-${selectedBoss}`
    try {
      rankings = Cache.getRankingsCache(cacheKey)
      if (!rankings) {
        rankings = await API.getCompRankings(selectedBoss)
        Cache.cacheRankings(cacheKey, rankings)
      }
    } catch (err) {
      error = err.message
      rankings = null
    } finally {
      loading = false
    }
  }

  $: if (selectedBoss) loadComp()

  function specColor(specSlug) {
    // Deterministic hue from spec slug
    let hash = 0
    for (const ch of specSlug || '') hash = (hash * 31 + ch.charCodeAt(0)) & 0xffffffff
    return `hsl(${Math.abs(hash) % 360}, 60%, 55%)`
  }
</script>

<div class="comp-page">
  <h2>Comp Rankings</h2>

  <div class="controls">
    <select bind:value={selectedBoss}>
      <option value="">Select boss...</option>
      {#each worldData.bosses || [] as b (b.nameSlug)}
        <option value={b.nameSlug}>{b.name}</option>
      {/each}
    </select>
    {#if boss?.icon}
      <img
        src={getWowheadIconUrl(boss.icon, 'bosses')}
        alt={boss.name}
        class="boss-icon"
        on:error={(e) => (e.target.style.display = 'none')}
      />
    {/if}
  </div>

  {#if loading}
    <div class="status">Loading comp rankings…</div>
  {:else if error}
    <div class="status error">{error}</div>
  {:else if !selectedBoss}
    <div class="status muted">Select a boss to view comp rankings.</div>
  {:else if !rankings?.reports?.length}
    <div class="status muted">No comp rankings available.</div>
  {:else}
    <div class="reports">
      {#each rankings.reports as report, reportIdx (`${report.reportId}-${reportIdx}`)}
        <div class="report-card">
          <div class="report-header">
            <a
              href="https://www.warcraftlogs.com/reports/{report.reportId}"
              target="_blank"
              rel="noopener"
              class="report-link"
            >
              {report.title || 'Untitled report'}
            </a>
            <span class="duration">{report.fights?.[0]?.durationDisplay || 'N/A'}</span>
          </div>

          {#each report.fights || [] as fight (fight.id)}
            {#if fight.composition}
              <div class="comp-breakdown">
                <div class="spec-pills">
                  {#each Object.entries(fight.composition.specs || {}) as [slug, count] (slug)}
                    <span class="spec-pill" style="border-color: {specColor(slug)}">
                      {slug} ×{count}
                    </span>
                  {/each}
                </div>
                <div class="role-pills">
                  {#each Object.entries(fight.composition.roles || {}) as [role, count] (role)}
                    <span class="role-pill">{role}: {count}</span>
                  {/each}
                </div>
              </div>
              <div class="players">
                {#each fight.players || [] as player (player.id ?? player.name)}
                  <span class="player-chip">{player.name}</span>
                {/each}
              </div>
            {/if}
          {/each}
        </div>
      {/each}
    </div>
  {/if}
</div>

<style>
  .comp-page {
    max-width: 900px;
  }

  h2 {
    margin: 0 0 1.5rem;
    font-size: 1.8rem;
  }

  .controls {
    display: flex;
    align-items: center;
    gap: 1rem;
    margin-bottom: 1.5rem;
  }

  .controls select {
    padding: 0.5rem 1rem;
    background: #2a2a2a;
    border: 1px solid #444;
    color: #fff;
    border-radius: 0.5rem;
    font-size: 1rem;
    min-width: 220px;
  }

  .boss-icon {
    width: 40px;
    height: 40px;
    border-radius: 5px;
    border: 1px solid #444;
  }

  .status {
    padding: 2rem;
    text-align: center;
    color: #aaa;
  }
  .status.error {
    color: #f66;
  }
  .status.muted {
    color: #666;
  }

  .reports {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
  }

  .report-card {
    background: #1e1e2e;
    border: 1px solid #333;
    border-radius: 0.6rem;
    padding: 1rem 1.25rem;
  }

  .report-header {
    display: flex;
    justify-content: space-between;
    align-items: baseline;
    margin-bottom: 0.5rem;
  }

  .report-link {
    color: #8ab4f8;
    text-decoration: none;
    font-weight: 500;
  }
  .report-link:hover {
    text-decoration: underline;
  }
  .duration {
    color: #aaa;
    font-size: 0.875rem;
  }

  .comp-breakdown {
    display: flex;
    flex-direction: column;
    gap: 0.35rem;
    margin-bottom: 0.5rem;
  }

  .spec-pills,
  .role-pills {
    display: flex;
    flex-wrap: wrap;
    gap: 0.35rem;
  }

  .spec-pill {
    font-size: 0.75rem;
    padding: 0.15rem 0.5rem;
    border-radius: 999px;
    border: 1px solid #555;
    color: #ccc;
  }

  .role-pill {
    font-size: 0.75rem;
    padding: 0.15rem 0.5rem;
    border-radius: 4px;
    background: #2a2a3e;
    color: #aaa;
  }

  .players {
    display: flex;
    flex-wrap: wrap;
    gap: 0.4rem;
    margin-top: 0.25rem;
  }

  .player-chip {
    font-size: 0.8rem;
    padding: 0.1rem 0.5rem;
    background: #2a2a2a;
    border-radius: 4px;
    color: #ddd;
  }
</style>
