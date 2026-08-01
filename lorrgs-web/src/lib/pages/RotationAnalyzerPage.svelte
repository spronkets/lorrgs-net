<script lang="ts">
  import * as API from '../api'

  let reportCode = ''
  let lookup = null
  let selectedFightId = ''
  let selectedSourceId = ''
  let parsePercentile = ''
  let itemLevel = ''

  let loadingLookup = false
  let loadingAnalysis = false
  let error = ''
  let analysis = null

  async function runLookup() {
    if (!reportCode.trim()) {
      error = 'Enter a Warcraft Logs report code first.'
      return
    }

    loadingLookup = true
    error = ''
    analysis = null

    try {
      lookup = await API.lookupReport(reportCode.trim())
      const fights = lookup?.reportData?.report?.fights || []
      const players = lookup?.reportData?.report?.masterData?.actors || []

      selectedFightId = fights.length ? String(fights[0].id) : ''
      selectedSourceId = players.length ? String(players[0].id) : ''
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to lookup report'
      lookup = null
    } finally {
      loadingLookup = false
    }
  }

  async function runAnalysis() {
    if (!reportCode.trim()) {
      error = 'Enter a Warcraft Logs report code first.'
      return
    }

    if (!selectedSourceId) {
      error = 'Select a player to analyze.'
      return
    }

    loadingAnalysis = true
    error = ''

    try {
      analysis = await API.analyzeRotation({
        reportCode: reportCode.trim(),
        fightId: selectedFightId ? Number(selectedFightId) : null,
        sourceId: Number(selectedSourceId),
        parsePercentile: parsePercentile ? Number(parsePercentile) : null,
        itemLevel: itemLevel ? Number(itemLevel) : null,
        eventLimit: 5000
      })
    } catch (err) {
      error = err instanceof Error ? err.message : 'Failed to analyze rotation'
      analysis = null
    } finally {
      loadingAnalysis = false
    }
  }

  $: fights = lookup?.reportData?.report?.fights || []
  $: players = lookup?.reportData?.report?.masterData?.actors || []
</script>

<div class="rotation-page">
  <h2>Rotation Analyzer</h2>
  <p class="subtitle">
    Works with any Warcraft Logs report across Retail, Classic, Era, Anniversary, and more.
  </p>

  <div class="panel">
    <label>
      Report Code
      <input bind:value={reportCode} placeholder="e.g. abcDEF123xyz" />
    </label>
    <button on:click={runLookup} disabled={loadingLookup}>
      {loadingLookup ? 'Looking up...' : 'Lookup Report'}
    </button>
  </div>

  {#if lookup}
    <div class="panel grid">
      <label>
        Fight
        <select bind:value={selectedFightId}>
          {#each fights as fight (fight.id)}
            <option value={String(fight.id)}>{fight.name} (ID {fight.id})</option>
          {/each}
        </select>
      </label>

      <label>
        Player
        <select bind:value={selectedSourceId}>
          {#each players as player (player.id)}
            <option value={String(player.id)}>{player.name} [{player.subType || 'Unknown'}]</option>
          {/each}
        </select>
      </label>

      <label>
        Parse Percentile (optional)
        <input bind:value={parsePercentile} type="number" min="0" max="100" placeholder="0-100" />
      </label>

      <label>
        Item Level (optional)
        <input bind:value={itemLevel} type="number" min="1" placeholder="e.g. 525" />
      </label>

      <button on:click={runAnalysis} disabled={loadingAnalysis}>
        {loadingAnalysis ? 'Analyzing...' : 'Analyze Rotation'}
      </button>
    </div>
  {/if}

  {#if error}
    <div class="error">{error}</div>
  {/if}

  {#if analysis}
    <div class="panel">
      <h3>Result: {analysis.verdict}</h3>
      <p>Score: {analysis.score}/100</p>
      <p>Player: {analysis.playerName} | Role: {analysis.roleHint} | Spec: {analysis.specHint}</p>
      <p>Fight: {analysis.fightName} | Casts/min: {analysis.castsPerMinute}</p>

      <h4>Notes</h4>
      <ul>
        {#each analysis.notes || [] as note, noteIdx (`${note}-${noteIdx}`)}
          <li>{note}</li>
        {/each}
      </ul>

      <h4>Top Ability Usage</h4>
      <div class="table">
        <div class="row header">
          <span>Ability</span><span>Casts</span><span>Share</span><span>Median Interval</span>
        </div>
        {#each analysis.abilitySummary || [] as row, rowIdx (`${row.ability}-${rowIdx}`)}
          <div class="row">
            <span>{row.ability}</span>
            <span>{row.castCount}</span>
            <span>{row.sharePercent}%</span>
            <span
              >{row.medianIntervalSeconds
                ? `${row.medianIntervalSeconds.toFixed(1)}s`
                : 'N/A'}</span
            >
          </div>
        {/each}
      </div>
    </div>
  {/if}
</div>

<style>
  .rotation-page {
    max-width: 1100px;
  }

  .subtitle {
    color: #b6b6b6;
    margin-top: -0.5rem;
    margin-bottom: 1rem;
  }

  .panel {
    border: 1px solid #333;
    background: #1a1a1a;
    border-radius: 0.5rem;
    padding: 1rem;
    margin-bottom: 1rem;
    display: flex;
    flex-direction: column;
    gap: 0.8rem;
  }

  .grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  }

  label {
    display: flex;
    flex-direction: column;
    gap: 0.35rem;
    color: #d5d5d5;
    font-size: 0.92rem;
  }

  input,
  select,
  button {
    background: #262626;
    color: #f1f1f1;
    border: 1px solid #454545;
    border-radius: 0.4rem;
    padding: 0.5rem 0.65rem;
  }

  button {
    cursor: pointer;
  }

  button:disabled {
    opacity: 0.6;
    cursor: default;
  }

  .error {
    background: #4a2020;
    border: 1px solid #8b3a3a;
    color: #ffafaf;
    padding: 0.75rem 1rem;
    border-radius: 0.4rem;
  }

  .table {
    border: 1px solid #333;
    border-radius: 0.4rem;
    overflow: hidden;
  }

  .row {
    display: grid;
    grid-template-columns: 2fr 0.7fr 0.7fr 1fr;
    gap: 0.6rem;
    padding: 0.5rem 0.7rem;
    border-bottom: 1px solid #2a2a2a;
    font-size: 0.9rem;
  }

  .row.header {
    font-weight: 700;
    background: #121212;
  }

  .row:last-child {
    border-bottom: none;
  }
</style>
