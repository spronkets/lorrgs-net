<script lang="ts">
  import type { RankingReport } from '../models/RankingReport';
  import { getRankingReportLink } from '../rankingUtils';

  let { report, edition, showReportName = true }: {
    report: Pick<RankingReport, 'reportId' | 'title' | 'guildName' | 'serverName' | 'serverRegion' | 'factionId' | 'fights'>;
    edition: string;
    showReportName?: boolean;
  } = $props();
  const faction = $derived(report.factionId === 1 ? 'Alliance' : report.factionId === 2 ? 'Horde' : null);
</script>

<div class="report-identity">
  <h3>
    {#if faction}
      <img class="faction-icon" src={`/images/wow/shared/factions/${faction.toLowerCase()}.png`}
        alt={faction} title={faction} />
    {/if}
    <a class:alliance={faction === 'Alliance'} class:horde={faction === 'Horde'}
      href={getRankingReportLink({ reportId: report.reportId, fights: report.fights }, edition)} target="_blank" rel="noopener noreferrer">
      <span class="guild-name" class:alliance={faction === 'Alliance'} class:horde={faction === 'Horde'}>{report.guildName || report.title}</span>
      <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor"
        stroke-width="2" aria-hidden="true">
        <path d="M14 3h7v7m0-7L10 14M10 3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-5" />
      </svg>
    </a>
    {#if showReportName && report.guildName}<span class="report-name">({report.title})</span>{/if}
  </h3>
  {#if report.serverRegion || report.serverName}
    <div class="raid-identity">
      {#if report.serverRegion}<span>{report.serverRegion.toUpperCase()}</span>{/if}
      {#if report.serverName}<span>{report.serverName}</span>{/if}
    </div>
  {/if}
</div>

<style>
  h3 {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.4rem;
    font-size: 1rem;
    line-height: 1.5;
    margin: 0;
  }
  a {
    display: inline-flex;
    align-items: center;
    gap: 3px;
    color: inherit;
    font-weight: 700;
    text-decoration: none;
  }
  a svg {
    display: block;
    align-self: center;
    flex-shrink: 0;
  }
  .alliance { color: #69aaff; }
  .horde { color: #ff6969; }
  .faction-icon {
    width: 14px;
    height: 14px;
    object-fit: contain;
    flex-shrink: 0;
  }
  .report-name,
  .raid-identity {
    color: #9cabb8;
    font-weight: 400;
  }
  .raid-identity {
    display: flex;
    align-items: center;
    gap: 0.35rem;
    font-size: 0.85rem;
    line-height: 1.5;
  }
</style>
