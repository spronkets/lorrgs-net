<script lang="ts">
  import Button from '../lib/components/Button.svelte';
  import * as WorldDataApi from '../lib/worldDataApi';

  import Alert from '../lib/components/Alert.svelte';
  import Spinner from '../lib/components/Spinner.svelte';
  import Input from '../lib/components/Input.svelte';
  import { onMount } from 'svelte';
  import * as API from '../lib/api';

  export let edition = 'retail';

  let reportCode = '';
  let lookup = null;
  let selectedFightId = '';
  let selectedSourceId = '';
  let parsePercentile = '';
  let itemLevel = '';

  let loadingLookup = false;
  let loadingAnalysis = false;
  let error = '';
  let analysis = null;
  let resolvedSpellNames: Record<number, string> = {};

  function parseSpellIdFromAbility(ability: unknown): number | null {
    if (typeof ability !== 'string') {
      return null;
    }

    const match = /^Spell:(\d+)$/.exec(ability.trim());
    if (!match) {
      return null;
    }

    const id = Number(match[1]);
    return Number.isInteger(id) && id > 0 ? id : null;
  }

  function readSpellId(row: unknown): number | null {
    if (row && typeof row === 'object') {
      const spellIdValue = (row as { spellId?: unknown }).spellId;
      if (typeof spellIdValue === 'number' && Number.isInteger(spellIdValue) && spellIdValue > 0) {
        return spellIdValue;
      }

      const abilityValue = (row as { ability?: unknown }).ability;
      return parseSpellIdFromAbility(abilityValue);
    }

    return null;
  }

  function formatAbilityLabel(ability: unknown): string {
    if (typeof ability !== 'string') {
      return 'Unknown';
    }

    const spellId = parseSpellIdFromAbility(ability);
    if (!spellId) {
      return ability;
    }

    const resolved = resolvedSpellNames[spellId];
    return resolved ? `${resolved} (#${spellId})` : `Unknown Spell (#${spellId})`;
  }

  function formatAbilityRowLabel(row: unknown): string {
    const spellId = readSpellId(row);
    if (spellId) {
      const resolved = resolvedSpellNames[spellId];
      return resolved ? `${resolved} (#${spellId})` : `Unknown Spell (#${spellId})`;
    }

    const ability =
      row && typeof row === 'object' && 'ability' in row
        ? (row as { ability?: unknown }).ability
        : 'Unknown';

    return formatAbilityLabel(ability);
  }

  async function resolveSpellNamesFromAnalysis(): Promise<void> {
    const rows = analysis?.abilitySummary;
    if (!Array.isArray(rows) || rows.length === 0) {
      resolvedSpellNames = {};
      return;
    }

    const spellIds = Array.from(
      new Set(
        rows.map((row) => readSpellId(row)).filter((id): id is number => typeof id === 'number')
      )
    );

    if (spellIds.length === 0) {
      resolvedSpellNames = {};
      return;
    }

    const entries = await Promise.all(
      spellIds.map(async (spellId) => {
        try {
          const spell = (await WorldDataApi.getSpell(spellId, edition)) as { name?: string };
          const name = typeof spell?.name === 'string' ? spell.name.trim() : '';
          return [spellId, name || `Spell ${spellId}`] as const;
        } catch {
          return [spellId, `Spell ${spellId}`] as const;
        }
      })
    );

    resolvedSpellNames = Object.fromEntries(entries);
  }

  async function initializeFromQuery() {
    const params = new URLSearchParams(window.location.search);
    const queryReportCode = String(params.get('reportCode') || '').trim();
    const queryFightId = String(params.get('fightId') || '').trim();
    const querySourceId = String(params.get('sourceId') || '').trim();
    const queryPlayerName = String(params.get('playerName') || '').trim();
    const queryPercentile = String(params.get('parsePercentile') || '').trim();
    const queryItemLevel = String(params.get('itemLevel') || '').trim();
    const shouldAutoAnalyze = String(params.get('autoAnalyze') || '').trim() === '1';

    if (!queryReportCode) {
      return;
    }

    reportCode = queryReportCode;

    if (queryPercentile) {
      parsePercentile = queryPercentile;
    }

    if (queryItemLevel) {
      itemLevel = queryItemLevel;
    }

    await runLookup();

    const hasFight = fights.some((fight) => String(fight.id) === queryFightId);
    if (queryFightId && hasFight) {
      selectedFightId = queryFightId;
    }

    const hasSource = players.some((player) => String(player.id) === querySourceId);
    if (querySourceId && hasSource) {
      selectedSourceId = querySourceId;
    } else if (queryPlayerName) {
      const matchingPlayers = players.filter(
        (player) => player.name?.toLowerCase() === queryPlayerName.toLowerCase()
      );
      if (matchingPlayers.length === 1) {
        selectedSourceId = String(matchingPlayers[0].id);
      } else {
        selectedSourceId = '';
        error = `Could not uniquely resolve ${queryPlayerName} in this report. Select the player manually.`;
        console.warn('Rotation player lookup did not resolve a unique actor', {
          reportCode: queryReportCode,
          playerName: queryPlayerName,
          matchCount: matchingPlayers.length
        });
        return;
      }
    } else if (querySourceId && !hasSource) {
      selectedSourceId = '';
      error = 'The linked player was not found in this report. Select the player manually.';
      console.warn('Rotation source ID was not found in report', {
        reportCode: queryReportCode,
        sourceId: querySourceId
      });
      return;
    }

    if (shouldAutoAnalyze && selectedSourceId) {
      await runAnalysis();
    }
  }

  async function runLookup() {
    if (!reportCode.trim()) {
      error = 'Enter a Warcraft Logs report code first.';
      return;
    }

    loadingLookup = true;
    error = '';
    analysis = null;

    try {
      lookup = await API.lookupReport(reportCode.trim());
      const fights = lookup?.reportData?.report?.fights || [];
      const players = lookup?.reportData?.report?.masterData?.actors || [];

      selectedFightId = fights.length ? String(fights[0].id) : '';
      selectedSourceId = players.length ? String(players[0].id) : '';
    } catch (err: unknown) {
      error = err instanceof Error ? err.message : 'Failed to lookup report';
      lookup = null;
    } finally {
      loadingLookup = false;
    }
  }

  async function runAnalysis() {
    if (!reportCode.trim()) {
      error = 'Enter a Warcraft Logs report code first.';
      return;
    }

    if (!selectedSourceId) {
      error = 'Select a player to analyze.';
      return;
    }

    loadingAnalysis = true;
    error = '';
    resolvedSpellNames = {};

    try {
      analysis = await API.analyzeRotation({
        reportCode: reportCode.trim(),
        fightId: selectedFightId ? Number(selectedFightId) : null,
        sourceId: Number(selectedSourceId),
        parsePercentile: parsePercentile ? Number(parsePercentile) : null,
        itemLevel: itemLevel ? Number(itemLevel) : null,
        eventLimit: 5000
      });
      await resolveSpellNamesFromAnalysis();
    } catch (err: unknown) {
      error = err instanceof Error ? err.message : 'Failed to analyze rotation';
      analysis = null;
      resolvedSpellNames = {};
    } finally {
      loadingAnalysis = false;
    }
  }

  $: fights = lookup?.reportData?.report?.fights || [];
  $: players = lookup?.reportData?.report?.masterData?.actors || [];

  onMount(() => {
    void initializeFromQuery();
  });
</script>

<div class="rotation-page">
  <h2>Rotation Analyzer</h2>
  <p class="subtitle">
    Works with any Warcraft Logs report across Retail, Classic, Era, Anniversary, and more.
  </p>

  <div class="panel">
    <Input label="Report Code" bind:value={reportCode} placeholder="e.g. abcDEF123xyz" />
    <Button
      variant="primary"
      size="small"
      class={['rotation-analyzer-page-button']}
      onclick={runLookup}
      disabled={loadingLookup}
    >
      {#if loadingLookup}<Spinner inline />{/if}
      {loadingLookup ? 'Looking up...' : 'Lookup Report'}
    </Button>
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

      <Input
        label="Parse Percentile (optional)"
        bind:value={parsePercentile}
        type="number"
        min="0"
        max="100"
        placeholder="0-100"
      />

      <Input
        label="Item Level (optional)"
        bind:value={itemLevel}
        type="number"
        min="1"
        placeholder="e.g. 525"
      />

      <Button
        variant="primary"
        size="small"
        class={['rotation-analyzer-page-button']}
        onclick={runAnalysis}
        disabled={loadingAnalysis}
      >
        {#if loadingAnalysis}<Spinner inline />{/if}
        {loadingAnalysis ? 'Analyzing...' : 'Analyze Rotation'}
      </Button>
    </div>
  {/if}

  {#if error}
    <Alert variant="error">{error}</Alert>
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
            <span>{formatAbilityRowLabel(row)}</span>
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

<style lang="scss">
  @use '../lib/styles/selection-tokens' as selectionTokens;

  .rotation-page {
    @include selectionTokens.apply-selection-tokens;

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
    @include selectionTokens.selection-label;

    display: flex;
    flex-direction: column;
    gap: 0.35rem;
  }

  select {
    @include selectionTokens.selection-dropdown;
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
