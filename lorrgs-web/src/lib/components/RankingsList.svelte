<script lang="ts">
  import Button from './Button.svelte';
  import { maska } from 'maska/svelte';
  import { killTimeMask } from '../killTimeMask';
  import Input from './Input.svelte';
  import Alert from './Alert.svelte';
  import { getRankingFilterError, toKillTimeInput, selectRankingReports } from '../rankingUtils';
  import type { RankingFilters } from '../models/rankings/RankingFilters';
  import type { RankingSort } from '../models/rankings/RankingSort';
  import type { RankingReport } from '../models/RankingReport';
  import type { SpecRankingData } from '../models/SpecRankingData';
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

  $: apiReports = Array.isArray(rankings?.reports) ? rankings.reports : ([] as RankingReport[]);
  $: filterError = getRankingFilterError(filters);
  $: visibleReports = selectRankingReports(apiReports, filters, sort);
</script>

<div class="rankings-list">
  <section class="analytics" aria-label="Ranking analytics">
    <div class="analytics-filters">
      <fieldset class="filter-group region-filter">
        <legend>Region</legend>
        {#each ['', 'us', 'eu', 'kr', 'tw', 'cn'] as region (region)}
          <Button
            variant="secondary-outline"
            size="small"
            class={['rankings-list-button']}
            type="button"
            aria-label={region ? `Region ${region.toUpperCase()}` : 'All regions'}
            aria-pressed={filters.region === region}
            onclick={() => (filters.region = region)}
            >{region ? region.toUpperCase() : 'All'}</Button
          >
        {/each}
      </fieldset>
      <fieldset class="filter-group duration-filter">
        <legend>Kill time</legend>
        <Input
          aria-label="Minimum kill time"
          action={(node) => maska(node, killTimeMask)}
          type="text"
          placeholder="m:ss"
          bind:value={filters.minDuration}
          onblur={() => (filters.minDuration = toKillTimeInput(filters.minDuration))}
        />
        <span aria-hidden="true">–</span>
        <Input
          aria-label="Maximum kill time"
          action={(node) => maska(node, killTimeMask)}
          type="text"
          placeholder="m:ss"
          bind:value={filters.maxDuration}
          onblur={() => (filters.maxDuration = toKillTimeInput(filters.maxDuration))}
        />
      </fieldset>
      <fieldset class="filter-group date-filter">
        <legend>Date range</legend>
        <Input aria-label="From date" type="date" bind:value={filters.fromDate} />
        <span aria-hidden="true">–</span>
        <Input aria-label="To date" type="date" bind:value={filters.toDate} />
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
      <Button
        variant="secondary-outline"
        size="small"
        class={['rankings-list-button']}
        type="button"
        onclick={resetFilters}>Reset filters</Button
      >
    </div>
    {#if filterError}<Alert variant="error">{filterError}</Alert>{/if}
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

<style lang="scss">
  @use '../styles/selection-tokens' as selectionTokens;

  .rankings-list {
    @include selectionTokens.apply-selection-tokens;
  }

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
    .analytics .filter-group :global(button.rankings-list-button) {
      flex: 1;
      min-width: 34px;
      min-height: 44px;
    }
    .analytics .duration-filter > :global(.form-field) {
      flex: 1;
      width: 0;
      min-width: 0;
    }
    .analytics .date-filter {
      flex-wrap: wrap;
    }
    .analytics .date-filter > :global(.form-field) {
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
    @include selectionTokens.selection-label;

    margin-bottom: 3px;
  }

  .duration-filter > :global(.form-field) {
    width: 105px;
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
    @include selectionTokens.selection-label;

    display: flex;
    flex-direction: column;
    gap: 0.3rem;
  }

  .analytics-filters select {
    @include selectionTokens.selection-dropdown;
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
</style>
