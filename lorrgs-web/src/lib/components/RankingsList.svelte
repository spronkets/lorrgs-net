<script lang="ts">
  import {
    formatRankingDuration,
    getRankingFilterError,
    selectRankingReports,
    summarizeRankingReports,
    type RankingFilters,
    type RankingSort
  } from '../rankingUtils';
  import type { RankingReport, SpecRankingData } from '../api';
  import RankingsTimeline from './RankingsTimeline.svelte';

  export let rankings: SpecRankingData | null = null;
  export let edition = '';
  export let bossIconName: string | undefined = undefined;

  let filters: RankingFilters = emptyFilters();
  let sort: RankingSort = 'rank';

  function emptyFilters(): RankingFilters {
    return { region: '', minDuration: '', maxDuration: '', fromDate: '', toDate: '' };
  }

  function resetFilters() {
    filters = emptyFilters();
    sort = 'rank';
  }

  function formatPerformance(value: number | null | undefined) {
    return typeof value === 'number' && Number.isFinite(value)
      ? value.toLocaleString(undefined, { maximumFractionDigits: 1 })
      : 'N/A';
  }

  $: apiReports = Array.isArray(rankings?.reports) ? rankings.reports : ([] as RankingReport[]);
  $: filterError = getRankingFilterError(filters);
  $: visibleReports = selectRankingReports(apiReports, filters, sort);
  $: summary = summarizeRankingReports(visibleReports.map((entry) => entry.report));
</script>

<div class="rankings-list">
  <section class="analytics" aria-label="Ranking analytics">
    <div class="analytics-filters">
      <fieldset class="filter-group">
        <legend>Region</legend>
        {#each ['', 'us', 'eu', 'kr', 'tw', 'cn'] as region (region)}
          <button
            type="button"
            aria-label={region ? `Region ${region.toUpperCase()}` : 'All regions'}
            aria-pressed={filters.region === region}
            on:click={() => (filters.region = region)}
            >{region ? region.toUpperCase() : 'All'}</button
          >
        {/each}
      </fieldset>
      <fieldset class="filter-group">
        <legend>Kill time</legend>
        <input
          aria-label="Minimum kill time"
          bind:value={filters.minDuration}
          placeholder="Min m:ss"
          class="kill-time"
        />
        <span aria-hidden="true">–</span>
        <input
          aria-label="Maximum kill time"
          bind:value={filters.maxDuration}
          placeholder="Max m:ss"
          class="kill-time"
        />
      </fieldset>
      <fieldset class="filter-group">
        <legend>Date range</legend>
        <input aria-label="From date" type="date" bind:value={filters.fromDate} />
        <span aria-hidden="true">–</span>
        <input aria-label="To date" type="date" bind:value={filters.toDate} />
      </fieldset>
      <label
        >Sort by
        <select bind:value={sort}>
          <option value="rank">Original rank</option>
          <option value="performance">Performance (highest)</option>
          <option value="duration">Kill time (shortest)</option>
          <option value="date">Date (newest)</option>
        </select>
      </label>
      <button type="button" on:click={resetFilters}>Reset filters</button>
    </div>
    {#if filterError}<p role="alert">{filterError}</p>{/if}
    <div class="analytics-summary" aria-live="polite">
      <span>Showing {summary.count} of {apiReports.length} loaded reports</span>
      <span
        >Best {rankings?.metric?.toUpperCase() || 'performance'}: {formatPerformance(
          summary.bestPerformance
        )}</span
      >
      <span>Median kill time: {formatRankingDuration(summary.medianDuration)}</span>
    </div>
  </section>

  {#key `${edition}:${rankings?.specSlug}:${rankings?.bossSlug}:${rankings?.updated}`}
    <RankingsTimeline
      reports={visibleReports}
      {edition}
      bossSlug={rankings?.bossSlug ?? ''}
      {bossIconName}
      difficulty={rankings?.difficulty ?? ''}
      specSlug={rankings?.specSlug ?? ''}
      classId={apiReports[0]?.players?.[0]?.classId ?? 0}
    />
  {/key}
</div>

<style>
  .filter-group {
    display: flex;
    gap: 3px;
    align-items: center;
    border: 0;
    margin: 0;
    padding: 0;
    min-width: 0;
  }
  .filter-group legend {
    font-size: 11px;
    margin-bottom: 3px;
  }
  .filter-group button[aria-pressed='true'] {
    border-color: #c64a85;
    box-shadow: inset 0 -2px #c64a85;
  }
  .analytics-filters button {
    cursor: pointer;
    transition:
      background 120ms,
      border-color 120ms,
      box-shadow 120ms;
  }
  .analytics-filters button:hover {
    background: #e2edf6;
    border-color: #687e94;
  }
  .analytics-filters button:focus-visible {
    outline: 2px solid #687e94;
    outline-offset: 2px;
  }
  :global(.theme-dark) .analytics-filters button:hover {
    background: #33465b;
    border-color: #a4b8cb;
  }
  :global(.theme-dark) .analytics-filters button:focus-visible {
    outline-color: #a4b8cb;
  }
  .kill-time {
    width: 80px;
  }
  .analytics {
    padding: 0.6rem;
    margin-bottom: 0.6rem;
    border: 1px solid #c2d8e7;
    border-radius: 0.5rem;
    background: #fff;
    color: #2a4b69;
  }

  .analytics-filters {
    display: flex;
    flex-wrap: wrap;
    gap: 0.5rem;
    align-items: end;
  }

  .analytics-filters label {
    display: flex;
    flex-direction: column;
    gap: 0.3rem;
    font-size: 0.75rem;
  }

  .analytics-filters input,
  .analytics-filters select,
  .analytics-filters button {
    padding: 0.3rem;
    border: 1px solid #8fc1e4;
    border-radius: 0.3rem;
    background: #f3f9ff;
    color: #2a4b69;
  }

  .analytics-summary {
    display: flex;
    flex-wrap: wrap;
    gap: 1rem;
    margin-top: 0.5rem;
    font-size: 0.75rem;
    font-weight: 600;
  }

  .rankings-list {
    animation: slideUp 0.3s ease-in-out;
  }

  @keyframes slideUp {
    from {
      opacity: 0;
      transform: translateY(10px);
    }
    to {
      opacity: 1;
      transform: translateY(0);
    }
  }

  :global(.theme-dark) .analytics {
    background: #13263a;
    border-color: #355472;
    color: #c9e4f8;
  }

  :global(.theme-dark) .analytics-filters input,
  :global(.theme-dark) .analytics-filters select,
  :global(.theme-dark) .analytics-filters button {
    background: #102032;
    border-color: #355472;
    color: #c9e4f8;
    color-scheme: dark;
  }
</style>
