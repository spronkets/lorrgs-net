<script lang="ts">
  import { getDisplayPercentile, getSortedReports } from '../rankingUtils'

  export let rankings = {}

  let sortBy = 'percentile'
  let sortDesc = true

  function toggleSort(field) {
    if (sortBy === field) {
      sortDesc = !sortDesc
    } else {
      sortBy = field
      sortDesc = true
    }
  }

  $: sortedReports = getSortedReports(rankings.reports, sortBy, sortDesc)
</script>

<div class="rankings-list">
  <div class="rankings-header">
    <div class="rankings-info">
      <h3>{rankings.specSlug} - {rankings.bossSlug}</h3>
      <p>{rankings.difficulty} • Metric: {rankings.metric}</p>
      <p class="meta">
        Average Percentile: {getDisplayPercentile(rankings.percentile)}
        <span class="updated">
          Updated: {rankings.updated ? new Date(rankings.updated).toLocaleDateString() : 'N/A'}
        </span>
      </p>
    </div>
  </div>

  <div class="table-container">
    <table class="rankings-table">
      <thead>
        <tr>
          <th>#</th>
          <th>Report</th>
          <th>
            <button
              class:active={sortBy === 'percentile'}
              on:click={() => toggleSort('percentile')}
            >
              Percentile {sortBy === 'percentile' ? (sortDesc ? '↓' : '↑') : ''}
            </button>
          </th>
          <th>
            <button class:active={sortBy === 'dps'} on:click={() => toggleSort('dps')}>
              Performance {sortBy === 'dps' ? (sortDesc ? '↓' : '↑') : ''}
            </button>
          </th>
          <th>
            <button class:active={sortBy === 'duration'} on:click={() => toggleSort('duration')}>
              Duration {sortBy === 'duration' ? (sortDesc ? '↓' : '↑') : ''}
            </button>
          </th>
          <th>Players</th>
          <th>Kill</th>
        </tr>
      </thead>
      <tbody>
        {#each sortedReports as report, idx (report.reportId ?? report.title ?? idx)}
          <tr class:killed={report.fights?.[0]?.isKill}>
            <td class="rank">{idx + 1}</td>
            <td class="report">
              <a
                href="https://www.warcraftlogs.com/reports/{report.reportId}"
                target="_blank"
                rel="noopener"
              >
                {report.title || 'Untitled report'}
              </a>
            </td>
            <td class="percentile"
              >{report.percentile ? `${report.percentile.toFixed(1)}%` : 'N/A'}</td
            >
            <td class="performance">
              {report.players?.[0]?.performance || 'N/A'}
            </td>
            <td class="duration">{report.durationDisplay || 'N/A'}</td>
            <td class="players">{report.players?.length || 0}</td>
            <td class="kill">
              {#if report.fights?.[0]?.isKill}
                <span class="badge kill">✓</span>
              {:else}
                <span class="badge wipe">✗</span>
              {/if}
            </td>
          </tr>
        {/each}
      </tbody>
    </table>
  </div>
</div>

<style>
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

  .rankings-header {
    background: #1a1a1a;
    border: 1px solid #333;
    border-radius: 0.5rem;
    padding: 1.5rem;
    margin-bottom: 1.5rem;
  }

  .rankings-info h3 {
    margin: 0 0 0.5rem 0;
    font-size: 1.3rem;
  }

  .rankings-info p {
    margin: 0;
    color: #999;
    font-size: 0.9rem;
  }

  .meta {
    margin-top: 0.5rem !important;
    display: flex;
    justify-content: space-between;
    align-items: center;
  }

  .updated {
    color: #666;
    font-size: 0.8rem;
  }

  .table-container {
    overflow-x: auto;
    border: 1px solid #333;
    border-radius: 0.5rem;
  }

  .rankings-table {
    width: 100%;
    border-collapse: collapse;
    font-size: 0.9rem;
  }

  .rankings-table thead {
    background: #1a1a1a;
    border-bottom: 1px solid #333;
  }

  .rankings-table th {
    padding: 0.75rem;
    text-align: left;
    font-weight: 600;
    color: #ccc;
  }

  .rankings-table th button {
    background: transparent;
    border: none;
    color: #ccc;
    cursor: pointer;
    font-weight: 600;
    padding: 0;
  }

  .rankings-table th button:hover {
    color: #fff;
    text-decoration: underline;
  }

  .rankings-table th button.active {
    color: #fff;
  }

  .rankings-table tbody tr {
    border-bottom: 1px solid #2a2a2a;
    transition: background 0.2s;
  }

  .rankings-table tbody tr:hover {
    background: #1a1a1a;
  }

  .rankings-table tbody tr.killed {
    background: rgba(76, 175, 80, 0.05);
  }

  .rankings-table td {
    padding: 0.75rem;
  }

  .rank {
    color: #999;
    font-weight: 600;
    width: 40px;
  }

  .report a {
    color: #64b5f6;
    text-decoration: none;
    word-break: break-word;
  }

  .report a:hover {
    text-decoration: underline;
  }

  .percentile {
    font-weight: 600;
    color: #fff;
  }

  .performance {
    color: #90ee90;
  }

  .duration {
    color: #daa520;
  }

  .players {
    text-align: center;
  }

  .kill {
    text-align: center;
  }

  .badge {
    display: inline-block;
    padding: 0.25rem 0.5rem;
    border-radius: 0.25rem;
    font-size: 0.8rem;
    font-weight: 600;
  }

  .badge.kill {
    background: rgba(76, 175, 80, 0.3);
    color: #4caf50;
  }

  .badge.wipe {
    background: rgba(244, 67, 54, 0.3);
    color: #f44336;
  }
</style>
