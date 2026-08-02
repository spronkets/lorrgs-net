<script lang="ts">
  import { getDisplayPercentile } from '../rankingUtils';
  import { getBlizzardClassColor } from '../classColors';
  import { getSpecIconUrl } from '../wowAssets';
  import type { RankingReport, SpecRankingData } from '../api';

  export let rankings: SpecRankingData | null = null;
  export let specLabel = '';
  export let bossLabel = '';
  export let edition = '';

  const classSlugById = {
    1: 'warrior',
    2: 'paladin',
    3: 'hunter',
    4: 'rogue',
    5: 'priest',
    6: 'death-knight',
    7: 'shaman',
    8: 'mage',
    9: 'warlock',
    10: 'monk',
    11: 'druid',
    12: 'demon-hunter',
    13: 'evoker'
  };

  function formatSlugLabel(value: string | null | undefined) {
    return String(value || '')
      .split('-')
      .filter(Boolean)
      .map((token) => token.charAt(0).toUpperCase() + token.slice(1))
      .join(' ');
  }

  function formatLogDate(value: string | Date | null | undefined) {
    if (!value) {
      return 'N/A';
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return 'N/A';
    }

    return date.toLocaleDateString();
  }

  function formatPrimaryPlayer(report: RankingReport) {
    const player = report?.players?.[0];
    if (!player?.name) {
      return 'N/A';
    }

    return player.name;
  }

  function formatPrimaryGuild(report: RankingReport) {
    const guild = report?.players?.[0]?.guildName;
    if (!guild) {
      return '';
    }

    return String(guild)
      .trim()
      .replace(/^<+/, '')
      .replace(/>+$/, '');
  }

  function getGuildFactionTone(report: RankingReport) {
    const player = report?.players?.[0];
    const factionId = Number(player?.factionId ?? player?.guildFaction ?? NaN);

    if (factionId === 0) {
      return 'alliance';
    }

    if (factionId === 1 || factionId === 2) {
      return 'horde';
    }

    const factionRaw = String(player?.faction || '').trim().toLowerCase();

    // Some ranking payloads return numeric faction markers.
    // Align with backend normalization: 0 => Alliance, 1/2 => Horde.
    if (factionRaw === '0') {
      return 'alliance';
    }

    if (factionRaw === '1' || factionRaw === '2') {
      return 'horde';
    }

    if (factionRaw === 'alliance') {
      return 'alliance';
    }

    if (factionRaw === 'horde') {
      return 'horde';
    }

    return 'neutral';
  }

  function getGuildFactionIcon(report: RankingReport) {
    const tone = getGuildFactionTone(report);
    if (tone === 'alliance') {
      return '/images/wow/shared/factions/alliance.png';
    }

    if (tone === 'horde') {
      return '/images/wow/shared/factions/horde.png';
    }

    return null;
  }

  function getWclHost() {
    const editionSlug = String(edition || '').trim().toLowerCase();
    if (editionSlug === 'anniversary') {
      return 'fresh.warcraftlogs.com';
    }

    if (editionSlug === 'mistsofpandaria') {
      return 'classic.warcraftlogs.com';
    }

    if (editionSlug === 'era') {
      return 'vanilla.warcraftlogs.com';
    }

    return 'www.warcraftlogs.com';
  }

  function normalizeServerRegionForUrl(value: string | null | undefined) {
    const normalized = String(value || '')
      .trim()
      .toLowerCase();

    if (!normalized) {
      return '';
    }

    if (normalized === 'na') {
      return 'us';
    }

    return normalized;
  }

  function getPlayerLink(report: RankingReport) {
    const player = report?.players?.[0];
    const name = String(player?.name || '').trim().toLowerCase();
    const region = normalizeServerRegionForUrl(player?.serverRegion);
    const realm = String(player?.serverName || player?.serverSlug || '').trim().toLowerCase();

    if (!name || !region || !realm) {
      return null;
    }

    return `https://${getWclHost()}/character/${encodeURIComponent(region)}/${encodeURIComponent(realm)}/${encodeURIComponent(name)}`;
  }

  function getGuildLink(report: RankingReport) {
    const player = report?.players?.[0];
    const guildName = formatPrimaryGuild(report).toLowerCase();
    const region = normalizeServerRegionForUrl(player?.serverRegion);
    const realm = String(player?.serverName || player?.serverSlug || '').trim().toLowerCase();

    if (!guildName || !region || !realm) {
      return null;
    }

    return `https://${getWclHost()}/guild/${encodeURIComponent(region)}/${encodeURIComponent(realm)}/${encodeURIComponent(guildName)}`;
  }

  function getGuildRegionLabel(report: RankingReport) {
    const player = report?.players?.[0];
    const region = normalizeServerRegionForUrl(player?.serverRegion);
    if (!region) {
      return '';
    }

    return region.toUpperCase();
  }

  function getPrimaryClassColor(report: RankingReport) {
    const classId = Number(report?.players?.[0]?.classId || 0);
    const classSlug = classSlugById[classId];
    return getBlizzardClassColor(classSlug, '#8aa5bc');
  }

  function getPrimaryClassSlug(report: RankingReport) {
    const classId = Number(report?.players?.[0]?.classId || 0);
    return classSlugById[classId] || 'other';
  }

  function getPrimarySpecSlug(report: RankingReport) {
    const classSlug = getPrimaryClassSlug(report);
    const playerSpecSlug = String(report?.players?.[0]?.specSlug || '')
      .trim()
      .toLowerCase();

    if (!playerSpecSlug) {
      return 'unknown';
    }

    const classSuffix = `-${classSlug}`;
    if (playerSpecSlug.endsWith(classSuffix)) {
      return playerSpecSlug.slice(0, -classSuffix.length);
    }

    return playerSpecSlug;
  }

  function hasBrightClassColor(report: RankingReport) {
    // Priest white needs a stronger shadow to remain readable on light backgrounds.
    const classId = Number(report?.players?.[0]?.classId || 0);
    return classId === 5;
  }

  $: apiReports = Array.isArray(rankings?.reports) ? rankings.reports : ([] as RankingReport[]);
  $: resolvedSpecLabel = specLabel || formatSlugLabel(rankings?.specSlug);
  $: resolvedBossLabel = bossLabel || formatSlugLabel(rankings?.bossSlug);
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
              <div
                class="player-name"
                class:bright-class={hasBrightClassColor(report)}
                style={`--player-class-color: ${getPrimaryClassColor(report)}`}
              >
                <img
                  class="player-spec-icon"
                  src={getSpecIconUrl(getPrimaryClassSlug(report), getPrimarySpecSlug(report))}
                  alt="Spec icon"
                  loading="lazy"
                  on:error={(event) => (event.currentTarget.style.display = 'none')}
                />
                {#if getPlayerLink(report)}
                  <a
                    class="player-link"
                    href={getPlayerLink(report)}
                    target="_blank"
                    rel="noopener"
                  >{formatPrimaryPlayer(report)}</a>
                {:else}
                  <span>{formatPrimaryPlayer(report)}</span>
                {/if}
              </div>
              {#if formatPrimaryGuild(report)}
                <div
                  class="player-guild"
                  class:guild-alliance={getGuildFactionTone(report) === 'alliance'}
                  class:guild-horde={getGuildFactionTone(report) === 'horde'}
                >
                  {#if getGuildFactionIcon(report)}
                    <img
                      class="guild-faction-icon"
                      src={getGuildFactionIcon(report)}
                      alt={getGuildFactionTone(report) === 'alliance' ? 'Alliance' : 'Horde'}
                      loading="lazy"
                    />
                  {/if}
                  {#if getGuildLink(report)}
                    <a
                      class="guild-link"
                      href={getGuildLink(report)}
                      target="_blank"
                      rel="noopener"
                    >{formatPrimaryGuild(report)}</a>
                  {:else}
                    {formatPrimaryGuild(report)}
                  {/if}
                  {#if getGuildRegionLabel(report)}
                    <span class="guild-region">({getGuildRegionLabel(report)})</span>
                  {/if}
                </div>
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
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: 0.15rem;
    color: #3e607f;
  }

  .player-name {
    display: inline-flex;
    align-items: center;
    gap: 0.4rem;
    color: var(--player-class-color, #8aa5bc);
    font-weight: 600;
    white-space: nowrap;
    text-shadow: 0 1px 1px rgba(0, 0, 0, 0.35);
  }

  .player-link {
    color: inherit;
    text-decoration: none;
  }

  .player-link:hover {
    text-decoration: underline;
  }

  .player-spec-icon {
    width: 1rem;
    height: 1rem;
    border-radius: 0.2rem;
    flex: 0 0 1rem;
    object-fit: cover;
    box-shadow: 0 1px 2px rgba(0, 0, 0, 0.25);
  }

  .player-name.bright-class {
    text-shadow:
      0 0 1px rgba(0, 0, 0, 0.65),
      0 1px 2px rgba(0, 0, 0, 0.45);
  }

  .player-guild {
    display: flex;
    align-items: center;
    gap: 0.35rem;
    color: #6c89a2;
    font-size: 0.8rem;
    white-space: nowrap;
  }

  .guild-link {
    color: inherit;
    text-decoration: none;
  }

  .guild-region {
    opacity: 0.85;
    font-size: 0.74rem;
    letter-spacing: 0.03em;
  }

  .guild-link:hover {
    text-decoration: underline;
  }

  .guild-faction-icon {
    width: 0.85rem;
    height: 0.85rem;
    object-fit: contain;
    flex: 0 0 0.85rem;
    border-radius: 0.12rem;
    opacity: 0.95;
  }

  .player-guild.guild-alliance {
    color: #002956;
  }

  .player-guild.guild-horde {
    color: #a70100;
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

  :global(.theme-dark) .player-guild.guild-alliance {
    color: #4f7aa7;
  }

  :global(.theme-dark) .player-guild.guild-horde {
    color: #d3514f;
  }

  :global(.theme-dark) .player-name {
    text-shadow: 0 1px 1px rgba(0, 0, 0, 0.6);
  }

  :global(.theme-dark) .percentile {
    color: #e6f2ff;
  }
</style>
