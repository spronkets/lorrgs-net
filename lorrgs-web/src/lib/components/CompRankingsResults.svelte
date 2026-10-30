<script lang="ts">
  import type { CompRankingFilters } from '../models/CompRankingFilters';
  import type { CompRankingData } from '../models/CompRankingData';
  import type { RankingReport } from '../models/RankingReport';
  import type { WorldDataSnapshot } from '../models/WorldDataSnapshot';
  import RankingsTimeline from './RankingsTimeline.svelte';
  import Spinner from './Spinner.svelte';
  import { getBlizzardClassColor } from '../classColors';
  import { timelineTime } from '../timelineUtils';
  import { getRankingReportLink, getTopPercentLabel } from '../rankingUtils';
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
  let initialLoading = $state(true);
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
      .filter((player) => (!roleFilter || role(player) === roleFilter) &&
        (!specFilter || player.specSlug === specFilter))
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
  {#if initialLoading}
    <Spinner
      loadingText={`Loading raid compositions and cooldown timelines… ${loader.loaded.size} of ${Math.min(3, rankings.reports.length)} reports ready.`}
    />
  {:else}
    {#if loader.stopped}
      <p role="alert">
        Some report details could not be loaded. Retry those rows or load another kill when ready.
      </p>
    {/if}
    {#if !matches.length && !pendingCount}<p>
        No inspected kills match these filters on this page.
      </p>{/if}
    {#each rankings.reports as summary, index (key(summary))}
      {@const report = loader.loaded.get(key(summary)) ?? summary}
      {@const fight = report.fights?.[0]}
      {@const guildName = report.guildName || summary.guildName}
      {@const serverName = report.serverName || summary.serverName}
      {@const region = (report.serverRegion || summary.serverRegion)?.toUpperCase()}
      {@const factionId = report.factionId ?? summary.factionId}
      {@const faction = factionId === 1 ? 'Alliance' : factionId === 2 ? 'Horde' : null}
      {@const selectedRole = selectedRoles[key(summary)] ?? ''}
      {@const selectedSpec = selectedSpecs[key(summary)] ?? ''}
      {@const roles =
        fight?.composition?.roles ?? fight?.roleCounts ?? summary.fights?.[0]?.roleCounts ?? {}}
      {@const playerCount =
        report.players?.length || Object.values(roles).reduce((sum, count) => sum + count, 0)}
      {#if durationMatches(summary) && compositionMatches(report)}
        <article data-position={(rankings.page - 1) * 50 + index + 1}>
          <div class="heading">
            <div class="title-block">
              <h3>
                {#if faction}
                  <img
                    class="faction-icon"
                    src={`/images/wow/shared/factions/${faction.toLowerCase()}.png`}
                    alt={faction}
                    title={faction}
                  />
                {/if}
                <a
                  class:alliance={faction === 'Alliance'}
                  class:horde={faction === 'Horde'}
                  href={getRankingReportLink({ ...report, players: [] }, edition)}
                  target="_blank"
                  rel="noopener noreferrer"
                >
                  <span
                    class="guild-name"
                    class:alliance={faction === 'Alliance'}
                    class:horde={faction === 'Horde'}>{guildName || 'Unknown guild'}</span
                  >
                  <svg
                    width="11"
                    height="11"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    stroke-width="2"
                    aria-hidden="true"
                  >
                    <path
                      d="M14 3h7v7m0-7L10 14M10 3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-5"
                    />
                  </svg>
                </a>
                {#if loader.loaded.has(key(summary))}
                  <span class="report-name">({report.title})</span>
                {/if}
              </h3>
              <div class="raid-details">
                <span class="raid-identity">
                  <span>{region || '—'}</span>
                  {#if serverName}<span>{serverName}</span>{/if}
                </span>
              </div>
            </div>
            <div class="stats">
              <div class="date-line">
                <span
                  class="efficiency-summary"
                  title={fight?.execution?.totalParses != null
                    ? `Efficiency ranking out of ${fight.execution.totalParses.toLocaleString()} ranked kills`
                    : 'Efficiency ranking unavailable'}
                >
                  Efficiency
                  {#if fight?.execution?.rank != null}
                    <span class="result-value"
                      >#{fight.execution.rank}{fight.execution.rankPercent != null
                        ? ` (${getTopPercentLabel(fight.execution.rankPercent)})`
                        : ''}</span
                    >
                  {:else}
                    unavailable
                  {/if}
                </span>
                <span
                  >Kill time <span class="result-value">{timelineTime(report.duration)}</span></span
                >
                <span
                  class="speed-summary"
                  title={fight?.speed?.totalParses != null
                    ? `Speed ranking out of ${fight.speed.totalParses.toLocaleString()} ranked kills`
                    : 'Speed ranking unavailable'}
                >
                  {#if fight?.speed?.rank != null}
                    Speed <span class="result-value"
                      >#{fight.speed.rank}{#if fight.speed.rankPercent != null}
                        {` (${getTopPercentLabel(fight.speed.rankPercent)})`}{/if}</span
                    >
                  {:else}
                    Speed unavailable
                  {/if}
                </span>
                <time datetime={report.startTime}
                  >{new Date(report.startTime).toLocaleDateString()}</time
                >
              </div>
              <span
                >{(fight?.damageTaken ?? 0).toLocaleString()} damage taken{#if (fight?.deaths ?? 0) > 0}
                  ({fight?.deaths} deaths){/if}</span
              >
            </div>
          </div>
          <div class="composition-heading">
            <h4>Raid composition</h4>
            <div class="roster-filters" role="group" aria-label="Roster roles">
              <button
                type="button"
                aria-pressed={!selectedRole && !selectedSpec}
                onclick={() => {
                  selectedRoles[key(summary)] = '';
                  selectedSpecs[key(summary)] = '';
                }}>All: {playerCount}</button
              >
              {#each Object.entries(roles) as [code, count] (code)}
                <button
                  type="button"
                  aria-pressed={selectedRole === code}
                  onclick={() => {
                    selectedRoles[key(summary)] = code;
                    selectedSpecs[key(summary)] = '';
                  }}
                  >{worldData.roles?.find((role) => role.code === code)?.name ??
                    roleNames[code] ??
                    code}: {count}</button
                >
              {/each}
            </div>
          </div>
          {#if !loader.loaded.has(key(summary))}
            <p>Class/spec composition pending · roster not inspected</p>
            {#if loader.errors.has(key(summary))}<p role="alert">
                {loader.errors.get(key(summary))}
              </p>{/if}
            <button
              type="button"
              class="load-details"
              disabled={loader.loadingKeys.has(key(summary))}
              onclick={() => loader.load(summary)}
            >
              {#if loader.loadingKeys.has(key(summary))}<Spinner inline />{/if}
              {loader.loadingKeys.has(key(summary))
                ? 'Loading details…'
                : loader.errors.has(key(summary))
                  ? 'Retry loading report'
                  : 'Load report'}
            </button>
          {:else}
            <div class="composition specs">
              {#each Object.entries(fight?.composition?.specs ?? {}) as [slug, count] (slug)}
                <button
                  type="button"
                  aria-pressed={selectedSpec === slug}
                  onclick={() => {
                    selectedSpecs[key(summary)] = selectedSpec === slug ? '' : slug;
                    selectedRoles[key(summary)] = '';
                  }}
                  class:dimmed={(!!selectedSpec && selectedSpec !== slug) || !!selectedRole &&
                    worldData.roles?.find(
                      (role) =>
                        role.id ===
                        worldData.specs?.find((spec) => spec.fullNameSlug === slug)?.roleId
                    )?.code !== selectedRole}
                  style={`border-color: ${color(slug)}`}
                  >{#if specIcon(slug)}<img
                      src={specIcon(slug)}
                      alt=""
                    />{/if}{worldData.specs?.find((spec) => spec.fullNameSlug === slug)?.fullName ??
                    slug} ×{count}</button
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
  .specs .dimmed {
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
  .title-block .raid-details {
    grid-column: 1;
    grid-row: 2;
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
  .report-name,
  .raid-details {
    color: #9cabb8;
    font-weight: 400;
  }
  h3 a {
    display: inline-flex;
    align-items: center;
    gap: 3px;
    font-weight: 700;
    text-decoration: none;
  }
  h3 a svg {
    display: block;
    align-self: center;
    flex-shrink: 0;
  }
  .raid-details,
  .raid-identity {
    display: flex;
    align-items: center;
    gap: 0.35rem;
  }
  .raid-details {
    flex-wrap: wrap;
    justify-content: space-between;
    font-size: 0.85rem;
    line-height: 1.5;
  }
  .raid-identity {
    min-width: 0;
    white-space: nowrap;
    font-size: inherit;
    font-weight: 400;
    color: #9cabb8;
  }
  .guild-name {
    font-weight: 700;
  }
  .guild-name.alliance,
  h3 a.alliance {
    color: #69aaff;
  }
  .guild-name.horde,
  h3 a.horde {
    color: #ff6969;
  }
  .faction-icon {
    width: 14px;
    height: 14px;
    object-fit: contain;
    flex-shrink: 0;
  }
  a {
    color: inherit;
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
  .composition button {
    display: inline-flex;
    align-items: center;
    gap: 0.3rem;
    padding: 0.2rem 0.4rem;
    border: 1px solid #687e94;
    border-radius: 0.3rem;
    color: inherit;
    background: transparent;
    font: inherit;
    cursor: pointer;
  }
  .composition button[aria-pressed='true'] {
    background: color-mix(in srgb, currentColor 12%, transparent);
    box-shadow: inset 0 -2px currentColor;
  }
  .composition button:focus-visible {
    outline: 2px solid currentColor;
    outline-offset: 3px;
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
