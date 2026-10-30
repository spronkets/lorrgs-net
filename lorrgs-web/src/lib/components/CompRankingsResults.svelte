<script lang="ts">
  import Button from './Button.svelte';
  import type { CompRankingFilters } from '../models/CompRankingFilters';
  import type { CompRankingData } from '../models/CompRankingData';
  import type { RankingReport } from '../models/RankingReport';
  import type { WorldDataSnapshot } from '../models/WorldDataSnapshot';
  import RankingsTimeline from './RankingsTimeline.svelte';
  import Alert from './Alert.svelte';
  import ReportIdentity from './ReportIdentity.svelte';
  import Spinner from './Spinner.svelte';
  import { getBlizzardClassColor } from '../classColors';
  import { timelineTime } from '../timelineUtils';
  import { getTopPercentLabel } from '../rankingUtils';
  import { SvelteSet } from 'svelte/reactivity';
  import { getSpecIconUrl } from '../wowAssets';
  import { onMount } from 'svelte';
  import { CompDetailLoader, compKey, matchesCompExpressions } from '../compDetailLoader.svelte';
  import { untrack } from 'svelte';
  type Props = {
    rankings: CompRankingData;
    worldData: WorldDataSnapshot;
    edition: string;
    bossIconName?: string;
    filters?: CompRankingFilters;
    refresh?: boolean;
    manualMode?: boolean;
    onprogress?: (_progress: { ready: boolean; pending: boolean }) => void;
  };
  let {
    rankings,
    worldData,
    edition,
    bossIconName,
    filters = { roles: [], specs: [], minSeconds: 0, maxSeconds: 0 },
    refresh = false,
    manualMode = false,
    onprogress
  }: Props = $props();
  const loader = new CompDetailLoader(
    untrack(() => edition),
    untrack(() => refresh)
  );
  let initialLoading = $state(true);
  $effect(() => {
    onprogress?.({
      ready: loader.loaded.size === rankings.reports.length,
      pending: initialLoading || loader.loadingKeys.size > 0
    });
  });
  onMount(() => {
    void loader.start(rankings.reports).then(() => {
      initialLoading = false;
    });
    return () => loader.dispose();
  });
  const expanded = new SvelteSet<string>();
  let selectedRoles = $state<Record<string, string>>({});
  let selectedSpecs = $state<Record<string, string>>({});
  const roleNames: Record<string, string> = {
    tank: 'Tanks',
    heal: 'Healers',
    mdps: 'Melee DPS',
    rdps: 'Ranged DPS'
  };
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
  function rosterReports(report: RankingReport, roleFilter: string, specFilter: string) {
    const roleOrder = ['tank', 'heal', 'mdps', 'rdps'];
    const role = (player: NonNullable<RankingReport['players']>[number]) =>
      player.roleCode ??
      worldData.roles?.find(
        (role) =>
          role.id === worldData.specs?.find((spec) => spec.fullNameSlug === player.specSlug)?.roleId
      )?.code ??
      '';
    return [...(report.players ?? [])]
      .filter(
        (player) =>
          (!roleFilter || role(player) === roleFilter) &&
          (!specFilter || player.specSlug === specFilter)
      )
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

<div class="comp-results" aria-busy={initialLoading}>
  {#if initialLoading && !onprogress}
    <Spinner
      loadingText={`Loading raid compositions and cooldown timelines… ${loader.loaded.size} of ${Math.min(3, rankings.reports.length)} reports ready.`}
    />
  {:else if !initialLoading}
    {#if loader.stopped}
      <Alert variant="error">
        Some report details could not be loaded. Retry those rows or load another kill when ready.
      </Alert>
    {/if}
    {#if !matches.length && !pendingCount}<p>
        No inspected kills match these filters on this page.
      </p>{/if}
    {#each rankings.reports as summary, index (key(summary))}
      {@const report = loader.loaded.get(key(summary)) ?? summary}
      {@const fight = report.fights?.[0]}
      {@const guildName = report.guildName || summary.guildName}
      {@const serverName = report.serverName || summary.serverName}
      {@const factionId = report.factionId ?? summary.factionId}
      {@const selectedRole = selectedRoles[key(summary)] ?? ''}
      {@const selectedSpec = selectedSpecs[key(summary)] ?? ''}
      {@const roles =
        fight?.composition?.roles ?? fight?.roleCounts ?? summary.fights?.[0]?.roleCounts ?? {}}
      {@const playerCount =
        report.players?.length || Object.values(roles).reduce((sum, count) => sum + count, 0)}
      {#if durationMatches(summary) && compositionMatches(report)}
        <article data-position={(rankings.page - 1) * 50 + index + 1}>
          {#snippet reportHeading()}
            <div class="heading">
              <div class="title-block">
                {#if manualMode}
                  <h3>
                    {fight?.isKill ? 'Kill' : 'Wipe'} #{fight?.fightId}
                  </h3>
                {:else}
                  <div class="report-identity">
                    <ReportIdentity
                      report={{
                        ...report,
                        guildName: guildName || 'Unknown guild',
                        serverName,
                        serverRegion: report.serverRegion || summary.serverRegion || '—',
                        factionId
                      }}
                      {edition}
                      showReportName={loader.loaded.has(key(summary))}
                    />
                  </div>
                {/if}
              </div>
              <div class="stats">
                <div class="date-line">
                  {#if !manualMode}<span
                      class="efficiency-summary"
                      title={fight?.execution?.totalParses != null
                        ? `Efficiency ranking out of ${fight.execution.totalParses.toLocaleString()} ranked kills`
                        : 'Efficiency ranking unavailable'}
                    >
                      Efficiency
                      {#if fight?.execution?.rank != null}
                        <span class="result-value"
                          >#{fight.execution.isApproximate ? '~' : ''}{fight.execution.rank}{fight
                            .execution.rankPercent != null
                            ? ` (${getTopPercentLabel(fight.execution.rankPercent)})`
                            : ''}</span
                        >
                      {:else}
                        unavailable
                      {/if}
                    </span>{/if}
                  <span
                    >{manualMode && !fight?.isKill ? 'Wipe time' : 'Kill time'}
                    <span class="result-value">{timelineTime(report.duration)}</span></span
                  >
                  {#if !manualMode}<span
                      class="speed-summary"
                      title={fight?.speed?.totalParses != null
                        ? `Speed ranking out of ${fight.speed.totalParses.toLocaleString()} ranked kills`
                        : 'Speed ranking unavailable'}
                    >
                      {#if fight?.speed?.rank != null}
                        Speed <span class="result-value"
                          >#{fight.speed.isApproximate ? '~' : ''}{fight.speed
                            .rank}{#if fight.speed.rankPercent != null}
                            {` (${getTopPercentLabel(fight.speed.rankPercent)})`}{/if}</span
                        >
                      {:else}
                        Speed unavailable
                      {/if}
                    </span>{/if}
                  <time datetime={report.startTime}
                    >{new Date(report.startTime).toLocaleDateString()}</time
                  >
                </div>
                <span
                  >{fight?.damageTaken != null
                    ? `${fight.damageTaken.toLocaleString()} damage taken`
                    : 'Damage taken unavailable'} · {fight?.deaths != null
                    ? `${fight.deaths} deaths`
                    : 'Deaths unavailable'}</span
                >
              </div>
            </div>
          {/snippet}
          {#snippet reportContent()}
            <div class="composition-heading">
              <h4>Raid composition</h4>
              <div class="roster-filters" role="group" aria-label="Roster roles">
                <Button
                  variant="secondary-outline"
                  size="small"
                  class={['comp-rankings-results-button']}
                  type="button"
                  aria-pressed={!selectedRole && !selectedSpec}
                  onclick={() => {
                    selectedRoles[key(summary)] = '';
                    selectedSpecs[key(summary)] = '';
                  }}>All: {playerCount}</Button
                >
                {#each Object.entries(roles) as [code, count] (code)}
                  <Button
                    variant="secondary-outline"
                    size="small"
                    class={['comp-rankings-results-button']}
                    type="button"
                    aria-pressed={selectedRole === code}
                    onclick={() => {
                      selectedRoles[key(summary)] = code;
                      selectedSpecs[key(summary)] = '';
                    }}
                    >{worldData.roles?.find((role) => role.code === code)?.name ??
                      roleNames[code] ??
                      code}: {count}</Button
                  >
                {/each}
              </div>
            </div>
            {#if !loader.loaded.has(key(summary))}
              <p>Class/spec composition pending · roster not inspected</p>
              {#if loader.errors.has(key(summary))}<Alert variant="error">
                  {loader.errors.get(key(summary))}
                </Alert>{/if}
              <Button
                variant="secondary-outline"
                size="small"
                type="button"
                class={['comp-rankings-results-button', `load-details`]}
                disabled={loader.loadingKeys.has(key(summary))}
                onclick={() => loader.load(summary)}
              >
                {#if loader.loadingKeys.has(key(summary))}<Spinner inline />{/if}
                {loader.loadingKeys.has(key(summary))
                  ? 'Loading details…'
                  : loader.errors.has(key(summary))
                    ? 'Retry loading report'
                    : 'Load report'}
              </Button>
            {:else}
              <div class="composition specs">
                {#each Object.entries(fight?.composition?.specs ?? {}) as [slug, count] (slug)}
                  <Button
                    variant="secondary-outline"
                    size="small"
                    class={[
                      'comp-rankings-results-button',
                      {
                        dimmed:
                          (!!selectedSpec && selectedSpec !== slug) ||
                          (!!selectedRole &&
                            worldData.roles?.find(
                              (role) =>
                                role.id ===
                                worldData.specs?.find((spec) => spec.fullNameSlug === slug)?.roleId
                            )?.code !== selectedRole)
                      }
                    ]}
                    type="button"
                    aria-pressed={selectedSpec === slug}
                    onclick={() => {
                      selectedSpecs[key(summary)] = selectedSpec === slug ? '' : slug;
                      selectedRoles[key(summary)] = '';
                    }}
                    style={`border-color: ${color(slug)}`}
                    >{#if specIcon(slug)}<img
                        src={specIcon(slug)}
                        alt=""
                      />{/if}{worldData.specs?.find((spec) => spec.fullNameSlug === slug)
                      ?.fullName ?? slug} ×{count}</Button
                  >
                {/each}
              </div>
              <details
                ontoggle={(event) => {
                  if (event.currentTarget.open) expanded.add(key(report));
                  else expanded.delete(key(report));
                }}
              >
                <summary
                  >Roster & cooldown timelines ({report.players?.length ?? 0} players)</summary
                >
                {#if expanded.has(key(report))}
                  <div class="roster-content">
                    {#key `${edition}:${key(report)}:${rankings.updated}`}
                      <RankingsTimeline
                        reports={rosterReports(report, selectedRole, selectedSpec)}
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
          {/snippet}
          {#if manualMode}
            <details class="pull-accordion">
              <summary aria-label={`${fight?.isKill ? 'Kill' : 'Wipe'} #${fight?.fightId}`}>
                {@render reportHeading()}
              </summary>
              {@render reportContent()}
            </details>
          {:else}
            {@render reportHeading()}
            {@render reportContent()}
          {/if}
        </article>
      {/if}
    {/each}
  {/if}
</div>

<style>
  .comp-results {
    display: grid;
    gap: 0.8rem;
  }
  .pull-accordion > summary {
    cursor: pointer;
  }
  details.pull-accordion {
    margin-top: 0;
    border: 0;
  }
  .pull-accordion > summary .heading {
    display: inline-grid;
    width: calc(100% - 1.5rem);
    vertical-align: middle;
  }
  article {
    border: 1px solid #687e94;
    border-radius: 0.4rem;
    padding: 0.8rem;
    min-width: 0;
  }
  h4 {
    margin: 0;
    font-size: 0.85rem;
  }
  .composition-heading {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: 0.6rem;
    margin-top: 1rem;
  }
  .specs :global(.dimmed.comp-rankings-results-button) {
    filter: grayscale(1);
    opacity: 0.35;
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
    display: grid;
    grid-template-columns: minmax(0, 1fr) auto;
    grid-template-rows: auto auto;
    align-items: center;
    gap: 0.3rem 1rem;
  }
  .title-block {
    display: contents;
  }
  .title-block h3 {
    grid-column: 1;
    grid-row: 1;
  }
  .report-identity {
    grid-column: 1;
    grid-row: 1 / 3;
  }
  h3 {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.4rem;
    font-size: 1rem;
    line-height: 1.5;
    margin: 0;
  }
  .stats {
    display: contents;
    font-size: 0.85rem;
    line-height: 1.5;
    font-weight: 400;
    color: #9cabb8;
    gap: 0.8rem 1.5rem;
    margin: 0;
    padding: 0;
    flex-shrink: 0;
  }
  .stats time {
    font-weight: 400;
  }
  .result-value {
    color: #dceeff;
  }
  .stats > span {
    grid-column: 2;
    grid-row: 2;
    justify-self: end;
  }
  .date-line {
    grid-column: 2;
    grid-row: 1;
    justify-self: end;
    display: flex;
    align-items: center;
    gap: 1rem;
    font-size: 1rem;
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
  }
</style>
