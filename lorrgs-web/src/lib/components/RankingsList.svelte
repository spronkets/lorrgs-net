<script lang="ts">
  import { getDisplayPercentile } from '../rankingUtils';

  export let rankings = {};
  export let specLabel = '';
  export let bossLabel = '';

  function formatSlugLabel(value) {
    return String(value || '')
      .split('-')
      .filter(Boolean)
      .map((token) => token.charAt(0).toUpperCase() + token.slice(1))
      .join(' ');
  }

  function formatLogDate(value) {
    if (!value) {
      return 'N/A';
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return 'N/A';
    }

    return date.toLocaleDateString();
  }

  function formatPrimaryPlayer(report) {
    const player = report?.players?.[0];
    if (!player?.name) {
      return 'N/A';
    }

    return player.name;
  }

  function formatPrimaryGuild(report) {
    const guild = report?.players?.[0]?.guildName;
    if (!guild) {
      return '';
    }

    return String(guild).trim();
  }

  $: apiReports = Array.isArray(rankings?.reports) ? rankings.reports : [];
  $: resolvedSpecLabel = specLabel || formatSlugLabel(rankings.specSlug);
  $: resolvedBossLabel = bossLabel || formatSlugLabel(rankings.bossSlug);
</script>

<div class="rankings-list">
  <div class="rankings-header">
    <div class="rankings-info">
      <h3>{resolvedSpecLabel} - {resolvedBossLabel}</h3>
      <p>{rankings.difficulty} • Metric: {rankings.metric}</p>
      <p class="meta">
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
          <th>Player</th>
          <th>Report</th>
          <th>Percentile</th>
          <th>Performance</th>
          <th>Duration</th>
          <th>Date</th>
        </tr>
      </thead>
      <tbody>
        {#each apiReports as report, idx (`${report.reportId ?? 'report'}-${report.startTime ?? 'time'}-${report.fights?.[0]?.fightId ?? 'fight'}-${idx}`)}
          <tr class:killed={report.fights?.[0]?.isKill}>
            <td class="rank">{idx + 1}</td>
            <td class="player">
              <div class="player-name">{formatPrimaryPlayer(report)}</div>
              {#if formatPrimaryGuild(report)}
                <div class="player-guild">&lt;{formatPrimaryGuild(report)}&gt;</div>
              {/if}
            </td>
            <td class="report">
              <a
                href="https://www.warcraftlogs.com/reports/{report.reportId}"
                target="_blank"
                rel="noopener"
              >
                {report.title || 'Untitled report'}
              </a>
            </td>
            <td class="percentile">{getDisplayPercentile(report.percentile)}</td>
            <td class="performance">
              {report.players?.[0]?.performance || 'N/A'}
            </td>
            <td class="duration">{report.durationDisplay || 'N/A'}</td>
            <td class="log-date">{formatLogDate(report.startTime)}</td>
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
    background: #ffffff;
    border: 1px solid #c2d8e7;
    border-radius: 0.5rem;
    padding: 1.5rem;
    margin-bottom: 1.5rem;
    box-shadow: 0 8px 20px rgba(19, 49, 78, 0.08);
  }

  .rankings-info h3 {
    margin: 0 0 0.5rem 0;
    font-size: 1.3rem;
  }

  .rankings-info p {
    margin: 0;
    color: #547694;
    font-size: 0.9rem;
  }

  .meta {
    margin-top: 0.5rem !important;
    display: flex;
    justify-content: flex-end;
    align-items: center;
  }

  .updated {
    color: #6989a5;
    font-size: 0.8rem;
  }

  .table-container {
    overflow-x: auto;
    border: 1px solid #c2d8e7;
    border-radius: 0.5rem;
    background: #ffffff;
  }

  .rankings-table {
    width: 100%;
    border-collapse: collapse;
    font-size: 0.9rem;
  }

  .rankings-table thead {
    background: #edf5fd;
    border-bottom: 1px solid #c2d8e7;
  }

  .rankings-table th {
    padding: 0.75rem;
    text-align: left;
    font-weight: 600;
    color: #2a4b69;
  }

  .rankings-table th button {
    background: transparent;
    border: none;
    color: #2a4b69;
    cursor: pointer;
    font-weight: 600;
    padding: 0;
  }

  .rankings-table th button:hover {
    color: #16344d;
    text-decoration: underline;
  }

  .rankings-table th button.active {
    color: #16344d;
  }

  .rankings-table tbody tr {
    border-bottom: 1px solid #d9e7f3;
    transition: background 0.2s;
  }

  .rankings-table tbody tr:hover {
    background: #f3f9ff;
  }

  .rankings-table tbody tr.killed {
    background: rgba(76, 175, 80, 0.05);
  }

  .rankings-table td {
    padding: 0.75rem;
  }

  .rank {
    color: #5f7f9b;
    font-weight: 600;
    width: 40px;
  }

  .report a {
    color: #64b5f6;
    text-decoration: none;
    word-break: break-word;
  }

  .player {
    color: #3e607f;
  }

  .player-name {
    white-space: nowrap;
  }

  .player-guild {
    margin-top: 0.15rem;
    color: #6c89a2;
    font-size: 0.8rem;
    white-space: nowrap;
  }

  .report a:hover {
    text-decoration: underline;
  }

  .percentile {
    font-weight: 600;
    color: #16344d;
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

  :global(.theme-dark) .rankings-header {
    background: #13263a;
    border-color: #355472;
    box-shadow: 0 8px 20px rgba(2, 10, 18, 0.35);
  }

  :global(.theme-dark) .rankings-info h3 {
    color: #e6f2ff;
  }

  :global(.theme-dark) .rankings-info p {
    color: #9ec0da;
  }

  :global(.theme-dark) .updated {
    color: #82a9c8;
  }

  :global(.theme-dark) .table-container {
    border-color: #355472;
    background: #13263a;
  }

  :global(.theme-dark) .rankings-table thead {
    background: #102032;
    border-bottom-color: #355472;
  }

  :global(.theme-dark) .rankings-table th,
  :global(.theme-dark) .rankings-table th button {
    color: #c9e4f8;
  }

  :global(.theme-dark) .rankings-table th button:hover,
  :global(.theme-dark) .rankings-table th button.active {
    color: #ecf7ff;
  }

  :global(.theme-dark) .rankings-table tbody tr {
    border-bottom-color: #27415d;
  }

  :global(.theme-dark) .rankings-table tbody tr:hover {
    background: #18324b;
  }

  :global(.theme-dark) .rank {
    color: #8db5d6;
  }

  :global(.theme-dark) .report a {
    color: #8ec9ff;
  }

  :global(.theme-dark) .player-guild {
    color: #99b7cf;
  }

  :global(.theme-dark) .percentile {
    color: #e6f2ff;
  }
</style>
