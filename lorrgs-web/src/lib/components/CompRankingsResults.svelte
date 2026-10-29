<script lang="ts">
  import type { CompRankingData, RankingReport, WorldDataSnapshot } from '../api';
  import RankingsTimeline from './RankingsTimeline.svelte';
  import { getBlizzardClassColor } from '../classColors';
  import { timelineTime } from '../timelineUtils';
  import { getRankingReportLink, getTopPercentLabel } from '../rankingUtils';
  import { SvelteSet } from 'svelte/reactivity';
  import { getSpecIconUrl } from '../wowAssets';
  import { onMount } from 'svelte';
  import { CompDetailLoader, compKey, matchesCompExpressions } from '../compDetailLoader.svelte';
  import { untrack } from 'svelte';
  type Filters = { roles: string[]; specs: string[]; minSeconds: number; maxSeconds: number };
  type Props = {
    rankings: CompRankingData;
    worldData: WorldDataSnapshot;
    edition: string;
    bossIconName?: string;
    filters?: Filters;
    refresh?: boolean;
  };
  let {
    rankings,
    worldData,
    edition,
    bossIconName,
    filters = { roles: [], specs: [], minSeconds: 0, maxSeconds: 0 },
    refresh = false
  }: Props = $props();
  const loader = new CompDetailLoader(
    untrack(() => edition),
    untrack(() => refresh)
  );
  onMount(() => {
    void loader.start(rankings.reports);
    return () => loader.dispose();
  });
  const expanded = new SvelteSet<string>();
  let selectedRole = $state('');
  function key(report: RankingReport) {
    return compKey(report);
  }
  function durationMatches(report: RankingReport) {
    return (
      report.duration >= filters.minSeconds * 1000 &&
      (!filters.maxSeconds || report.duration <= filters.maxSeconds * 1000)
    );
  }
  function compositionMatches(report: RankingReport) {
    const composition = report.fights?.[0]?.composition;
    return (
      !composition ||
      (matchesCompExpressions(composition.roles, filters.roles) &&
        matchesCompExpressions(composition.specs, filters.specs))
    );
  }
  const inspected = $derived(rankings.reports.filter((report) => loader.loaded.has(key(report))));
  const matches = $derived(
    inspected.filter(
      (report) => durationMatches(report) && compositionMatches(loader.loaded.get(key(report))!)
    )
  );
  const pendingCount = $derived(
    rankings.reports.filter((report) => durationMatches(report) && !loader.loaded.has(key(report)))
      .length
  );
  function executionRank(report: RankingReport) {
    const rank = (loader.loaded.get(key(report)) ?? report).fights?.[0]?.execution?.rank;
    return typeof rank === 'number' && Number.isFinite(rank) && rank > 0
      ? rank
      : Number.POSITIVE_INFINITY;
  }
  const sortedReports = $derived(
    [...rankings.reports].sort((left, right) => executionRank(left) - executionRank(right))
  );
  function rosterReports(report: RankingReport, roleFilter: string) {
    const roleOrder = ['tank', 'heal', 'mdps', 'rdps'];
    const role = (player: NonNullable<RankingReport['players']>[number]) =>
      player.roleCode ??
      worldData.roles?.find(
        (role) =>
          role.id === worldData.specs?.find((spec) => spec.fullNameSlug === player.specSlug)?.roleId
      )?.code ??
      '';
    return [...(report.players ?? [])]
      .filter((player) => !roleFilter || role(player) === roleFilter)
      .sort(
        (left, right) =>
          Math.min(2, roleOrder.indexOf(role(left))) -
            Math.min(2, roleOrder.indexOf(role(right))) ||
          (right.parsePercentile ?? -1) - (left.parsePercentile ?? -1) ||
          left.name.localeCompare(right.name)
      )
      .map((player, index) => ({
        rank: index + 1,
        report: {
          ...report,
          players: [{ ...player, roleCode: role(player) }],
          timeline: player.timeline
        }
      }));
  }
  function color(slug: string) {
    const spec = worldData.specs?.find((item) => item.fullNameSlug === slug);
    const cls = worldData.classes?.find((item) => item.id === spec?.classId);
    return getBlizzardClassColor(cls?.nameSlug, cls?.color ?? '#7ba4bf');
  }
  function specIcon(slug: string) {
    const spec = worldData.specs?.find((item) => item.fullNameSlug === slug);
    const cls = worldData.classes?.find((item) => item.id === spec?.classId);
    return spec && cls ? getSpecIconUrl(cls.nameSlug, spec.nameSlug) : '';
  }
</script>

<div class="comp-results">
  {#if loader.stopped}
    <p role="alert">
      Detail loading paused after an error. Retry that row or load another kill when ready.
    </p>
  {/if}
  {#if !matches.length && !pendingCount}<p>
      No inspected kills match these filters on this page.
    </p>{/if}
  {#each sortedReports as summary (key(summary))}
    {@const report = loader.loaded.get(key(summary)) ?? summary}
    {@const fight = report.fights?.[0]}
    {#if durationMatches(summary) && compositionMatches(report)}
      <article>
        <div class="heading">
          <h3>
            {Number.isFinite(executionRank(summary))
              ? `#${executionRank(summary)}`
              : 'Rank unavailable'} {report.title}
            {#if fight?.execution?.rankPercent != null}
              ({getTopPercentLabel(fight.execution.rankPercent)})
            {/if}
          </h3>
          <a
            href={getRankingReportLink({ ...report, players: [] }, edition)}
            target="_blank"
            rel="noopener noreferrer">Warcraft Logs ↗</a
          >
        </div>
        {#if report.guildName || summary.guildName || report.serverName || summary.serverName}
          <p class="raid-identity">
            {[
              report.guildName ?? summary.guildName,
              [
                report.serverName ?? summary.serverName,
                (report.serverRegion ?? summary.serverRegion)?.toUpperCase()
              ]
                .filter(Boolean)
                .join(' ')
            ]
              .filter(Boolean)
              .join(' · ')}
          </p>
        {/if}
        <div class="stats">
          {#each [{ label: 'Speed', ranking: fight?.speed }] as metric (metric.label)}
            <span
              title={metric.ranking?.totalParses != null
                ? `${metric.label} ranking out of ${metric.ranking.totalParses.toLocaleString()} ranked kills`
                : undefined}
            >
              <small>{metric.label}</small>
              <strong>
                {metric.ranking?.rank != null ? `#${metric.ranking.rank}` : 'Unavailable'}
                {#if metric.ranking?.rankPercent != null}
                  ({getTopPercentLabel(metric.ranking.rankPercent)})
                {/if}
              </strong>
            </span>
          {/each}
          <span><small>Kill time</small><strong>{timelineTime(report.duration)}</strong></span>
          <span><small>Deaths</small><strong>{fight?.deaths ?? 0}</strong></span>
          <span
            ><small>Damage taken</small><strong>{(fight?.damageTaken ?? 0).toLocaleString()}</strong
            ></span
          >
          <span
            ><small>Logged</small><time datetime={report.startTime}
              >{new Date(report.startTime).toLocaleDateString()}</time
            ></span
          >
        </div>
        {#if !loader.loaded.has(key(summary))}
          <p>Composition pending · roster not inspected</p>
          {#if loader.errors.has(key(summary))}<p role="alert">
              {loader.errors.get(key(summary))}
            </p>{/if}
          <button
            type="button"
            class="load-details"
            disabled={Boolean(loader.loadingKey)}
            onclick={() => loader.load(summary)}
          >
            {loader.loadingKey === key(summary)
              ? 'Loading details…'
              : loader.errors.has(key(summary))
                ? 'Retry kill details'
                : 'Load kill details'}
          </button>
        {:else}
          <h4>Raid composition</h4>
          <div class="composition" aria-label="Composition">
            {#each Object.entries(fight?.composition?.roles ?? {}) as [code, count] (code)}
              <span
                >{worldData.roles?.find((role) => role.code === code)?.name ?? code}: {count}</span
              >
            {/each}
          </div>
          <div class="composition specs">
            {#each Object.entries(fight?.composition?.specs ?? {}) as [slug, count] (slug)}
              <span style={`border-color: ${color(slug)}`}
                >{#if specIcon(slug)}<img src={specIcon(slug)} alt="" />{/if}{worldData.specs?.find(
                  (spec) => spec.fullNameSlug === slug
                )?.fullName ?? slug} ×{count}</span
              >
            {/each}
          </div>
          <details
            ontoggle={(event) => {
              if (event.currentTarget.open) expanded.add(key(report));
              else expanded.delete(key(report));
            }}
          >
            <summary>Roster & cooldown timelines ({report.players?.length ?? 0} players)</summary>
            {#if expanded.has(key(report))}
              <div class="roster-content">
                <div class="roster-filters" role="group" aria-label="Roster roles">
                  <button
                    type="button"
                    aria-pressed={!selectedRole}
                    onclick={() => (selectedRole = '')}>All players</button
                  >
                  {#each worldData.roles ?? [] as role (role.code)}
                    <button
                      type="button"
                      aria-pressed={selectedRole === role.code}
                      onclick={() => (selectedRole = role.code)}>{role.name}</button
                    >
                  {/each}
                </div>
                {#key `${edition}:${key(report)}:${rankings.updated}`}
                  <RankingsTimeline
                    reports={rosterReports(report, selectedRole)}
                    {edition}
                    bossSlug={rankings.bossSlug}
                    difficulty={rankings.difficulty}
                    compositionMode
                    {bossIconName}
                  />
                {/key}
              </div>
            {/if}
          </details>
        {/if}
      </article>
    {/if}
  {/each}
</div>

<style>
  .comp-results {
    display: grid;
    gap: 0.8rem;
  }
  article {
    border: 1px solid #687e94;
    border-radius: 0.4rem;
    padding: 0.8rem;
    min-width: 0;
  }
  h4 {
    margin: 1rem 0 0.5rem;
    font-size: 0.85rem;
  }
  .heading,
  .stats,
  .composition {
    display: flex;
    flex-wrap: wrap;
    gap: 0.6rem;
    align-items: center;
  }
  .heading {
    justify-content: space-between;
  }
  h3 {
    font-size: 1rem;
    margin: 0;
  }
  a {
    color: inherit;
  }
  .stats {
    font-size: 0.85rem;
    gap: 0.8rem 1.5rem;
    margin: 0.8rem 0;
    padding: 0.8rem 0;
    border-block: 1px solid color-mix(in srgb, currentColor 20%, transparent);
  }
  .stats > span {
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
  }
  .stats small {
    opacity: 0.75;
    font-size: 0.75rem;
  }
  .stats strong,
  .stats time {
    font-weight: 600;
    font-variant-numeric: tabular-nums;
  }
  details {
    margin-top: 1rem;
    border: 1px solid #687e94;
    border-radius: 0.4rem;
  }
  .composition {
    margin: 0.5rem 0;
    font-size: 0.8rem;
  }
  .composition span {
    display: inline-flex;
    align-items: center;
    gap: 0.3rem;
    padding: 0.2rem 0.4rem;
    border: 1px solid #687e94;
    border-radius: 0.3rem;
  }
  .composition img {
    width: 20px;
    height: 20px;
    border-radius: 2px;
  }
  summary {
    cursor: pointer;
    padding: 0.9rem 1rem;
    min-height: 44px;
    box-sizing: border-box;
    font-weight: 600;
    background: color-mix(in srgb, currentColor 7%, transparent);
    border-radius: 0.4rem;
  }
  summary:hover {
    background: color-mix(in srgb, currentColor 12%, transparent);
  }
  summary:focus-visible {
    outline: 2px solid currentColor;
    outline-offset: 3px;
  }
  details[open] > summary {
    border-bottom: 1px solid #687e94;
    border-radius: 0.4rem 0.4rem 0 0;
  }
  .roster-content {
    padding: 1rem;
    min-width: 0;
  }
  @media (max-width: 600px) {
    .roster-content {
      padding: 0.6rem;
    }
  }
  .roster-filters {
    display: flex;
    flex-wrap: wrap;
    gap: 0.4rem;
    margin-bottom: 0.6rem;
  }
  .roster-filters button {
    color: inherit;
    background: transparent;
    border: 1px solid #687e94;
    border-radius: 0.3rem;
    padding: 0.4rem 0.6rem;
    cursor: pointer;
  }
  .roster-filters button[aria-pressed='true'] {
    border-color: #c64a85;
    box-shadow: inset 0 -2px #c64a85;
  }
  .load-details {
    color: inherit;
    background: transparent;
    border: 1px solid #687e94;
    border-radius: 0.3rem;
    padding: 0.5rem 0.7rem;
    cursor: pointer;
  }
</style>
