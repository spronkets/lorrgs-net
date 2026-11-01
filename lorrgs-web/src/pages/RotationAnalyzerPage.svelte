<script lang="ts">
  import * as WorldDataApi from '../lib/worldDataApi';
  import { getBossPortraitUrl } from '../lib/bossPortraits';
  import { spellIconUrl } from '../lib/timelineUtils';
  import { slugifyBossName } from '../lib/raidCatalog';

  import Alert from '../lib/components/Alert.svelte';
  import BossSelector from '../lib/components/BossSelector.svelte';
  import Spinner from '../lib/components/Spinner.svelte';

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
  let resolvedSpellIcons: Record<number, string | null> = {};

  function formatClock(secondsValue: unknown): string {
    const seconds = typeof secondsValue === 'number' && Number.isFinite(secondsValue) ? secondsValue : 0;
    const rounded = Math.max(0, Math.round(seconds));
    const minutes = Math.floor(rounded / 60);
    const remainder = String(rounded % 60).padStart(2, '0');
    return `${minutes}:${remainder}`;
  }

  function spellLabelById(spellId: number): string {
    const resolved = resolvedSpellNames[spellId];
    return resolved ? `${resolved} (#${spellId})` : `Spell #${spellId}`;
  }

  function spellIconById(spellId: number): string | null {
    return resolvedSpellIcons[spellId] ?? null;
  }

  function throughputNoun(roleHint: unknown): string {
    if (typeof roleHint !== 'string') {
      return 'damage';
    }

    const normalized = roleHint.toLowerCase();
    if (normalized === 'healer') {
      return 'healing';
    }

    if (normalized === 'tank') {
      return 'damage';
    }

    return 'damage';
  }

  function cooldownLabels(spellIds: unknown): string {
    if (!Array.isArray(spellIds) || spellIds.length === 0) {
      return 'None detected';
    }

    const normalized = spellIds.filter((value): value is number => typeof value === 'number');
    return normalized.length ? normalized.map((spellId) => spellLabelById(spellId)).join(', ') : 'None detected';
  }

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
    return resolved ? `${resolved} (#${spellId})` : `Spell ${spellId} (#${spellId})`;
  }

  function formatAbilityRowLabel(row: unknown): string {
    const spellId = readSpellId(row);
    if (spellId) {
      const resolved = resolvedSpellNames[spellId];
      return resolved ? `${resolved} (#${spellId})` : `Spell ${spellId} (#${spellId})`;
    }

    const ability =
      row && typeof row === 'object' && 'ability' in row
        ? (row as { ability?: unknown }).ability
        : 'Unknown';

    return formatAbilityLabel(ability);
  }

  async function resolveSpellNamesFromAnalysis(): Promise<void> {
    const abilityRows = Array.isArray(analysis?.abilitySummary) ? analysis.abilitySummary : [];
    const damageRows = Array.isArray(analysis?.damageSummary) ? analysis.damageSummary : [];
    const phaseRows = Array.isArray(analysis?.phaseSummaries) ? analysis.phaseSummaries : [];
    const metadataRows = Array.isArray(analysis?.spellMetadata) ? analysis.spellMetadata : [];
    if (abilityRows.length === 0 && damageRows.length === 0 && phaseRows.length === 0) {
      resolvedSpellNames = {};
      resolvedSpellIcons = {};
      return;
    }

    const spellIds = Array.from(
      new Set(
        [
          ...abilityRows.map((row) => readSpellId(row)),
          ...damageRows.map((row) => readSpellId(row)),
          ...phaseRows.flatMap((phase) =>
            Array.isArray(phase?.topAbilities)
              ? phase.topAbilities.map((row) => readSpellId(row))
              : []
          ),
          ...phaseRows.flatMap((phase) =>
            Array.isArray(phase?.topDamageAbilities)
              ? phase.topDamageAbilities.map((row) => readSpellId(row))
              : []
          ),
          ...phaseRows.flatMap((phase) =>
            Array.isArray(phase?.cooldownSpellIds)
              ? phase.cooldownSpellIds.filter((id): id is number => typeof id === 'number')
              : []
          )
        ].filter((id): id is number => typeof id === 'number')
      )
    );

    if (spellIds.length === 0) {
      resolvedSpellNames = {};
      resolvedSpellIcons = {};
      return;
    }

    const namesFromMetadata: Record<number, string> = {};
    const iconsFromMetadata: Record<number, string | null> = {};

    for (const row of metadataRows) {
      const spellId = readSpellId(row);
      if (!spellId) {
        continue;
      }

      const name =
        row && typeof row === 'object' && 'name' in row ? (row as { name?: unknown }).name : null;
      const icon =
        row && typeof row === 'object' && 'icon' in row ? (row as { icon?: unknown }).icon : null;
      const normalizedName = typeof name === 'string' ? name.trim() : '';
      const normalizedIcon = typeof icon === 'string' && icon.trim() ? spellIconUrl(icon.trim()) : null;

      namesFromMetadata[spellId] = normalizedName || `Spell ${spellId}`;
      iconsFromMetadata[spellId] = normalizedIcon;
    }

    const unresolvedSpellIds = spellIds.filter((spellId) => !namesFromMetadata[spellId]);
    if (unresolvedSpellIds.length === 0) {
      resolvedSpellNames = namesFromMetadata;
      resolvedSpellIcons = iconsFromMetadata;
      return;
    }

    const entries = await Promise.all(
      unresolvedSpellIds.map(async (spellId) => {
        try {
          const spell = (await WorldDataApi.getSpell(spellId, edition)) as { name?: string; icon?: string };
          const name = typeof spell?.name === 'string' ? spell.name.trim() : '';
          const icon =
            typeof spell?.icon === 'string' && spell.icon.trim()
              ? spellIconUrl(spell.icon.trim())
              : null;
          return [spellId, { name: name || `Spell ${spellId}`, icon }] as const;
        } catch {
          return [spellId, { name: `Spell ${spellId}`, icon: null }] as const;
        }
      })
    );

    resolvedSpellNames = {
      ...namesFromMetadata,
      ...Object.fromEntries(entries.map(([spellId, value]) => [spellId, value.name]))
    };
    resolvedSpellIcons = {
      ...iconsFromMetadata,
      ...Object.fromEntries(entries.map(([spellId, value]) => [spellId, value.icon]))
    };
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
    resolvedSpellIcons = {};

    try {
      analysis = await API.analyzeRotation({
        reportCode: reportCode.trim(),
        fightId: selectedFightId ? Number(selectedFightId) : null,
        sourceId: Number(selectedSourceId),
        parsePercentile: parsePercentile ? Number(parsePercentile) : null,
        itemLevel: itemLevel ? Number(itemLevel) : null,
        autoResolveParsePercentile: true,
        eventLimit: 5000
      });
      await resolveSpellNamesFromAnalysis();
    } catch (err: unknown) {
      error = err instanceof Error ? err.message : 'Failed to analyze rotation';
      analysis = null;
      resolvedSpellNames = {};
      resolvedSpellIcons = {};
    } finally {
      loadingAnalysis = false;
    }
  }

  $: fights = lookup?.reportData?.report?.fights || [];
  $: players = lookup?.reportData?.report?.masterData?.actors || [];
  $: selectedFight = fights.find((fight) => String(fight.id) === selectedFightId) ?? null;
  $: fightOptions = fights.map((fight) => {
    const bossSlug = slugifyBossName(fight.name || '');
    const iconUrl = getBossPortraitUrl(bossSlug);
    return {
      slug: String(fight.id),
      name: `${fight.name} (ID ${fight.id})`,
      iconName: typeof iconUrl === 'string' && !iconUrl.endsWith('/boss.gif') ? iconUrl : null
    };
  });

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
    <div class="form-field">
      <label for="rotationanalyzerpage-field-1">Report Code</label>
      <div class="input-frame">
        <input
          id="rotationanalyzerpage-field-1"
          class="input"
          type="text"
          bind:value={reportCode}
          placeholder="e.g. abcDEF123xyz"
        />
      </div>
    </div>
    <button
      class={['button', ['rotation-analyzer-page-button']]}
      data-tone="primary"
      data-outline="false"
      data-size="small"
      type="button"
      onclick={runLookup}
      disabled={loadingLookup}
    >
      {#if loadingLookup}<Spinner inline />{/if}
      {loadingLookup ? 'Looking up...' : 'Lookup Report'}
    </button>
  </div>

  {#if lookup}
    <div class="panel grid">
      <BossSelector
        label="Fight"
        bosses={fightOptions}
        bind:value={selectedFightId}
        placeholder="Select a fight..."
      />

      <label>
        Player
        <select bind:value={selectedSourceId}>
          {#each players as player (player.id)}
            <option value={String(player.id)}
              >{player.name}{player.subType ? ` [${player.subType}]` : ''}</option
            >
          {/each}
        </select>
      </label>

      <button
        class={['button', ['rotation-analyzer-page-button']]}
        data-tone="primary"
        data-outline="false"
        data-size="small"
        type="button"
        onclick={runAnalysis}
        disabled={loadingAnalysis}
      >
        {#if loadingAnalysis}<Spinner inline />{/if}
        {loadingAnalysis ? 'Analyzing...' : 'Analyze Rotation'}
      </button>
    </div>
  {/if}

  {#if error}
    <Alert variant="error">{error}</Alert>
  {/if}

  {#if analysis}
    {@const throughput = throughputNoun(analysis.roleHint)}
    {@const throughputLabel = `${throughput.charAt(0).toUpperCase()}${throughput.slice(1)}`}
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

      <h4>Phase-aware Rotation</h4>
      {#if analysis.phaseSummaries?.length}
        <div class="phase-cards">
          {#each analysis.phaseSummaries as phaseSummary, phaseSummaryIdx (`${phaseSummary.phaseId}-${phaseSummaryIdx}`)}
            <article class="phase-card">
              <header class="phase-card__header">
                <span class="phase-badge">P{phaseSummary.phaseId}</span>
                <span>{formatClock(phaseSummary.startSeconds)} - {formatClock(phaseSummary.endSeconds)}</span>
                <span>{phaseSummary.castsPerMinute} casts/min</span>
              </header>
              <p class="phase-pattern">{phaseSummary.rotationPattern || 'No cast sequence detected.'}</p>
              <p class="phase-meta">
                Casts: {phaseSummary.castCount} · Duration: {phaseSummary.durationSeconds}s
              </p>
              <p class="phase-meta">
                {throughputLabel}: {Number(phaseSummary.totalDamage || 0).toLocaleString(undefined, { maximumFractionDigits: 0 })}
              </p>
              <p class="phase-meta"><strong>Cooldown windows:</strong> {cooldownLabels(phaseSummary.cooldownSpellIds)}</p>
              {#if phaseSummary.topDamageAbilities?.length}
                <div class="phase-damage">
                  <strong>Top {throughput}:</strong>
                  <ul>
                    {#each phaseSummary.topDamageAbilities as damageRow (`${damageRow.spellId}-${damageRow.ability}`)}
                      <li>
                        {#if damageRow.spellId && spellIconById(damageRow.spellId)}
                          <img src={spellIconById(damageRow.spellId)} alt="" loading="lazy" />
                        {/if}
                        <span>{formatAbilityRowLabel(damageRow)}</span>
                        <span>
                          {Number(damageRow.totalDamage || 0).toLocaleString(undefined, { maximumFractionDigits: 0 })}
                          ({damageRow.sharePercent}%)
                        </span>
                      </li>
                    {/each}
                  </ul>
                </div>
              {/if}
            </article>
          {/each}
        </div>
      {:else}
        <p class="phase-meta">No phase transitions detected for this fight.</p>
      {/if}

      <h4>Top Ability Usage</h4>
      <div class="table">
        <div class="row header">
          <span>Ability</span><span>Casts</span><span>Share</span><span>Median Interval</span>
        </div>
        {#each analysis.abilitySummary || [] as row, rowIdx (`${row.ability}-${rowIdx}`)}
          <div class="row">
            <span class="spell-cell">
              {#if row.spellId && spellIconById(row.spellId)}
                <img src={spellIconById(row.spellId)} alt="" loading="lazy" />
              {/if}
              {formatAbilityRowLabel(row)}
            </span>
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

      <h4>Top {throughputLabel} Contribution</h4>
      <div class="table">
        <div class="row header row-damage">
          <span>Ability</span><span>Total {throughput}</span><span>Share</span><span>Hits</span>
        </div>
        {#each analysis.damageSummary || [] as row, rowIdx (`${row.spellId}-${rowIdx}`)}
          <div class="row row-damage">
            <span class="spell-cell">
              {#if row.spellId && spellIconById(row.spellId)}
                <img src={spellIconById(row.spellId)} alt="" loading="lazy" />
              {/if}
              {formatAbilityRowLabel(row)}
            </span>
            <span>{Number(row.totalDamage || 0).toLocaleString(undefined, { maximumFractionDigits: 0 })}</span>
            <span>{row.sharePercent}%</span>
            <span>{row.hitCount}</span>
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

  .phase-cards {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
    gap: 0.8rem;
  }

  .phase-card {
    border: 1px solid #333;
    border-radius: 0.5rem;
    background: #141414;
    padding: 0.75rem;
  }

  .phase-card__header {
    display: flex;
    flex-wrap: wrap;
    gap: 0.5rem;
    align-items: center;
    font-weight: 600;
  }

  .phase-badge {
    color: #54bb68;
    border: 1px solid #54bb68;
    border-radius: 999px;
    font-size: 0.75rem;
    padding: 0.1rem 0.45rem;
  }

  .phase-pattern {
    margin: 0.55rem 0 0.4rem;
    font-size: 0.9rem;
  }

  .phase-meta {
    margin: 0;
    font-size: 0.85rem;
    color: #bebebe;
  }

  .phase-damage ul {
    list-style: none;
    margin: 0.35rem 0 0;
    padding: 0;
    display: flex;
    flex-direction: column;
    gap: 0.35rem;
  }

  .phase-damage li {
    display: grid;
    grid-template-columns: 1fr auto;
    gap: 0.6rem;
    align-items: center;
    font-size: 0.82rem;
    color: #d7d7d7;
  }

  .phase-damage li span:first-of-type {
    display: inline-flex;
    align-items: center;
    gap: 0.35rem;
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

  .row-damage {
    grid-template-columns: 2fr 1fr 0.7fr 0.7fr;
  }

  .spell-cell {
    display: inline-flex;
    align-items: center;
    gap: 0.35rem;
  }

  .spell-cell img,
  .phase-damage img {
    width: 18px;
    height: 18px;
    border-radius: 2px;
    object-fit: cover;
  }
</style>
