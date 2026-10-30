<script lang="ts">
  import { ExternalLink } from '@lucide/svelte';
  import type { RankingReport } from '../models/RankingReport';
  import { getRankingReportLink } from '../rankingUtils';

  let { report, edition, showReportName = true, regionNextToServer = false, reportNameInLink = false }: {
    report: Pick<RankingReport, 'reportId' | 'title' | 'guildName' | 'serverName' | 'serverRegion' | 'factionId' | 'fights'>;
    edition: string;
    showReportName?: boolean;
    regionNextToServer?: boolean;
    reportNameInLink?: boolean;
  } = $props();
  const faction = $derived(report.factionId === 1 ? 'Alliance' : report.factionId === 2 ? 'Horde' : null);
</script>

<div class="report-identity">
  <h3>
    {#if reportNameInLink}
      {#if report.guildName}
        <span class="guild-name faction-name" class:alliance={faction === 'Alliance'} class:horde={faction === 'Horde'} title={faction ?? undefined}>{report.guildName}</span>
      {/if}
      {#if showReportName || !report.guildName}
        <a class="report-link"
          href={getRankingReportLink({ reportId: report.reportId, fights: report.fights }, edition)}
          target="_blank" rel="noopener noreferrer">
          {report.title}
          <ExternalLink size={11} aria-hidden="true" />
        </a>
      {/if}
    {:else}
    <a class="faction-name" class:alliance={faction === 'Alliance'} class:horde={faction === 'Horde'} title={faction ?? undefined}
      href={getRankingReportLink({ reportId: report.reportId, fights: report.fights }, edition)} target="_blank" rel="noopener noreferrer">
      <span class="guild-name">{report.guildName || report.title}{!regionNextToServer && report.serverRegion ? ` ${report.serverRegion.toUpperCase()}` : ''}</span>
      <ExternalLink size={11} aria-hidden="true" />
    </a>
    {#if !reportNameInLink && showReportName && report.guildName}<span class="report-name">({report.title})</span>{/if}
    {/if}
  </h3>
  {#if report.serverName || (regionNextToServer && report.serverRegion)}
    <div class="raid-identity">
      {#if regionNextToServer}
        <span>{report.serverName ?? ''}{report.serverRegion ? ` ${report.serverRegion.toUpperCase()}` : ''}</span>
      {:else if report.serverName}
        <span>{report.serverName}</span>
      {/if}
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
  a :global(svg) {
    display: block;
    align-self: center;
    flex-shrink: 0;
  }
  .report-name,
  .raid-identity {
    color: var(--display-muted);
    font-weight: 400;
  }
  .report-link {
    color: var(--display-value, inherit);
    font-weight: 400;
  }
  :global(.theme-dark) .report-link {
    color: #ffffff;
  }
  .raid-identity {
    display: flex;
    align-items: center;
    gap: 0.35rem;
    font-size: 0.85rem;
    line-height: 1.5;
  }
</style>
