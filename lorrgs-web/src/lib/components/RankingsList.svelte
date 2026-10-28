<script lang="ts">
  import {
    getRankingFilterError,
    selectRankingReports,
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
  let reportLimit = 10;

  function emptyFilters(): RankingFilters {
    return { region: '', minDuration: '', maxDuration: '', fromDate: '', toDate: '' };
  }

  function resetFilters() {
    filters = emptyFilters();
    sort = 'rank';
  }

  $: apiReports = Array.isArray(rankings?.reports) ? rankings.reports : ([] as RankingReport[]);
  $: filterError = getRankingFilterError(filters);
  $: visibleReports = selectRankingReports(apiReports, filters, sort);
  $: {
    void visibleReports;
    reportLimit = 10;
  }
  $: displayedReports = visibleReports.slice(0, reportLimit);
  $: activeFilterCount = Object.values(filters).filter(Boolean).length + (sort !== 'rank' ? 1 : 0);
</script>

<div class="rankings-list">
  <section class="analytics" aria-label="Ranking analytics">
    <div class="analytics-filters">
      <fieldset class="filter-group region-filter">
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
      <fieldset class="filter-group duration-filter">
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
      <details class="advanced-filters">
        <summary>Filters{activeFilterCount ? ` (${activeFilterCount} active)` : ''}</summary>
        <div class="advanced-options">
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
      </details>
    </div>
    {#if filterError}<p role="alert">{filterError}</p>{/if}
  </section>

  {#key `${edition}:${rankings?.specSlug}:${rankings?.bossSlug}:${rankings?.updated}`}
    <RankingsTimeline
      reports={displayedReports}
      {edition}
      bossSlug={rankings?.bossSlug ?? ''}
      {bossIconName}
      difficulty={rankings?.difficulty ?? ''}
      specSlug={rankings?.specSlug ?? ''}
      classId={apiReports[0]?.players?.[0]?.classId ?? 0}
    />
  {/key}
  {#if displayedReports.length < visibleReports.length}
    <button class="load-more" type="button" on:click={() => (reportLimit += 10)}>
      Load more
    </button>
  {/if}
</div>

<style>
  @media (max-width: 600px) {
    .analytics .analytics-filters {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      gap: 8px;
    }
    .analytics .region-filter,
    .analytics .duration-filter {
      grid-column: 1 / -1;
    }
    .analytics .filter-group {
      gap: 6px;
    }
    .analytics .filter-group legend {
      margin-bottom: 5px;
    }
    .analytics .filter-group button {
      flex: 1;
      min-width: 34px;
      min-height: 38px;
    }
    .analytics .duration-filter input {
      flex: 1;
      width: 0;
      min-width: 0;
    }
    .analytics .advanced-filters[open] {
      grid-column: 1 / -1;
    }
    .analytics .filter-group input,
    .analytics .advanced-options select {
      min-height: 38px;
    }
    .analytics .advanced-filters summary {
      min-height: 38px;
      box-sizing: border-box;
      padding: 10px;
      border: 1px solid currentColor;
      border-radius: 4px;
    }
    .analytics .advanced-options {
      flex-direction: column;
      align-items: stretch;
    }
    .analytics .advanced-options .filter-group {
      flex-wrap: wrap;
    }
    .analytics .advanced-options input {
      min-width: 0;
      flex: 1;
    }
  }
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
    padding: 0.35rem 0;
    margin-bottom: 0.6rem;
    border: 0;
    background: transparent;
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

  .advanced-filters summary {
    cursor: pointer;
    font-size: 0.8rem;
    padding: 0.3rem;
  }
  .advanced-options {
    display: flex;
    flex-wrap: wrap;
    align-items: end;
    gap: 0.5rem;
    padding: 0.5rem 0;
  }
  .load-more {
    display: block;
    margin: 12px auto 0;
    min-height: 40px;
    padding: 8px 24px;
    border: 1px solid #8fc1e4;
    border-radius: 4px;
    background: #f3f9ff;
    color: #2a4b69;
    font: inherit;
    cursor: pointer;
  }
  .load-more:hover {
    border-color: #c64a85;
  }
  .load-more:focus-visible {
    outline: 2px solid #c64a85;
    outline-offset: 2px;
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
    background: transparent;
    color: #c9e4f8;
  }

  :global(.theme-dark) .analytics-filters input,
  :global(.theme-dark) .analytics-filters select,
  :global(.theme-dark) .analytics-filters button,
  :global(.theme-dark) .load-more {
    background: #102032;
    border-color: #355472;
    color: #c9e4f8;
    color-scheme: dark;
  }
</style>
