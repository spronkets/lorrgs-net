<script lang="ts">
  import { onDestroy, onMount } from 'svelte';
  import { SvelteMap } from 'svelte/reactivity';
  import { lookupReport } from '../lib/api';
  import {
    parseReportCodes,
    readReportOverview,
    manualRankingReport,
    MANUAL_REPORT_QUERY_KEYS,
    type ReportOverview
  } from '../lib/manualReports';
  import type { WorldDataSnapshot } from '../lib/models/WorldDataSnapshot';
  import Alert from '../lib/components/Alert.svelte';
  import Spinner from '../lib/components/Spinner.svelte';
  import PageHeader from '../lib/components/PageHeader.svelte';
  import EditionSelector from '../lib/components/EditionSelector.svelte';
  import { getVersionIconUrl } from '../lib/selectionIcons';
  import BossSelector from '../lib/components/BossSelector.svelte';
  import CompRankingsResults from '../lib/components/CompRankingsResults.svelte';
  import { DISABLED_EDITION_OPTIONS, EDITION_OPTIONS, slugifyBossName } from '../lib/raidCatalog';
  import { getBossPortraitUrl } from '../lib/bossPortraits';
  import ReportIdentity from '../lib/components/ReportIdentity.svelte';

  let {
    edition,
    worldData,
    selectedVersion = '',
    oneditionchange
  }: {
    edition: string;
    worldData: WorldDataSnapshot;
    selectedVersion?: string;
    oneditionchange?: (_version: string) => void;
  } = $props();
  let input = $state('');
  let hydrated = $state(false);
  let loading = $state(false);
  let error = $state('');
  const reports = new SvelteMap<string, ReportOverview>();
  const errors = new SvelteMap<string, string>();
  const progress = new SvelteMap<string, { ready: boolean; pending: boolean }>();
  let bosses = $state<Record<string, string>>({});
  const controller = new AbortController();
  onDestroy(() => controller.abort());
  const fightKey = (code: string, id: number) => `${code}:${id}`;

  function findBoss(encounterId: number | undefined, name: string) {
    return (
      worldData.bosses?.find((boss) => boss.id === encounterId) ??
      worldData.bosses?.find(
        (boss) => slugifyBossName(boss.name ?? boss.nameSlug) === slugifyBossName(name)
      )
    );
  }

  async function loadReports() {
    error = '';
    let codes: string[];
    try {
      codes = parseReportCodes(input);
    } catch (cause) {
      error = cause instanceof Error ? cause.message : 'Invalid report codes.';
      return;
    }
    loading = true;
    try {
      for (const code of codes) {
        if (controller.signal.aborted) break;
        if (reports.has(code)) continue;
        errors.delete(code);
        try {
          const report = readReportOverview(await lookupReport(code, controller.signal));
          if (controller.signal.aborted) break;
          reports.set(code, report);
          bosses[code] = String(report.fights[0]?.encounterID ?? '');
        } catch (cause) {
          if (controller.signal.aborted) break;
          errors.set(code, cause instanceof Error ? cause.message : 'Unable to load report.');
        }
      }
    } finally {
      loading = false;
    }
  }

  function removeReport(code: string) {
    reports.delete(code);
    delete bosses[code];
    for (const key of progress.keys()) if (key.startsWith(`${code}:`)) progress.delete(key);
  }

  async function restore() {
    const params = new URLSearchParams(window.location.search);
    const savedReports = params.getAll('report').map((entry) => entry.split(':'));
    const codes = savedReports.map(([code]) => code);
    if (codes.length) {
      input = codes.join('\n');
      await loadReports();
      if (controller.signal.aborted) return;
      const restoredBosses = new Set([
        ...params.getAll('reportBoss'),
        ...savedReports.filter(([, boss]) => boss).map(([code, boss]) => `${code}:${boss}`)
      ]);
      for (const report of reports.values()) {
        for (const fight of report.fights) {
          if (restoredBosses.has(fightKey(report.code, fight.encounterID)))
            bosses[report.code] = String(fight.encounterID);
        }
      }
    }
    hydrated = true;
  }

  onMount(() => {
    void restore();
  });

  $effect(() => {
    if (!hydrated || loading) return;
    const url = new URL(window.location.href);
    for (const key of MANUAL_REPORT_QUERY_KEYS) url.searchParams.delete(key);
    url.searchParams.set('page', 'reports');
    if (selectedVersion) url.searchParams.set('edition', selectedVersion);
    for (const code of [...reports.keys(), ...errors.keys()])
      url.searchParams.append('report', bosses[code] ? `${code}:${bosses[code]}` : code);
    window.history.replaceState({}, '', `${url.pathname}${url.search}${url.hash}`);
  });
</script>

<section class="reports-page">
  <PageHeader
    title="Load Report"
    description="Enter report codes or Warcraft Logs URLs to load every boss pull, full roster, and timeline."
  />
  <section class="report-edition" aria-label="Edition selection">
    <h3 class="edition-label">Edition</h3>
    <EditionSelector
      versions={EDITION_OPTIONS}
      disabledVersions={DISABLED_EDITION_OPTIONS}
      {selectedVersion}
      {getVersionIconUrl}
      onselect={oneditionchange}
    />
  </section>
  <form
    onsubmit={(event) => {
      event.preventDefault();
      void loadReports();
    }}
  >
    <div class="form-field">
      <label for="report-codes">Report codes or URLs</label><textarea
        id="report-codes"
        class="textarea"
        bind:value={input}
        rows="2"
        placeholder="Separate multiple reports with spaces, commas, or new lines"></textarea>
    </div>
    <button
      class="button"
      data-tone="primary"
      data-outline="false"
      data-size="medium"
      type="submit"
      disabled={loading || !edition}>{loading ? 'Loading reports…' : 'Load reports'}</button
    >
  </form>
  {#if !edition}<p>Select an edition before loading reports.</p>{/if}
  {#if loading}<Spinner loadingText="Loading report overviews…" />{/if}
  {#if error}<Alert variant="error">{error}</Alert>{/if}
  {#each [...errors] as [code, message] (code)}
    <Alert variant="error">{code}: {message} Edit the input and click Load reports to retry.</Alert>
  {/each}
  {#each [...reports] as [code, report] (code)}
    {@const bossOptions = [
      ...new Map(
        report.fights.map((fight) => [
          fight.encounterID,
          {
            slug: String(fight.encounterID),
            name: fight.name,
            iconName:
              findBoss(fight.encounterID, fight.name)?.icon ??
              getBossPortraitUrl(slugifyBossName(fight.name))
          }
        ])
      ).values()
    ]}
    <section class="report-selection" aria-label={`${report.title} fight selection`}>
      <div class="report-heading">
        <div class="report-title">
          <ReportIdentity report={{ ...report, reportId: code, fights: undefined }} {edition} />
          <p class="report-meta"><span>{report.zone?.name ?? 'Unknown raid'}</span> · {code}</p>
        </div>
        <button
          class="button"
          data-tone="danger"
          data-outline="true"
          data-size="medium"
          type="button"
          aria-label={`Remove report ${code}`}
          onclick={() => removeReport(code)}>Remove report</button
        >
      </div>
      {#if report.fights.length}
        <BossSelector bosses={bossOptions} bind:value={bosses[code]} />
        {@const visiblePulls = report.fights.filter(
          (pull) => String(pull.encounterID) === bosses[code]
        )}
        {@const readyCount = visiblePulls.filter(
          (pull) => progress.get(fightKey(code, pull.id))?.ready
        ).length}
        {#if visiblePulls.some((pull) => progress.get(fightKey(code, pull.id))?.pending ?? true)}
          <Spinner
            loadingText={`Loading raid compositions and cooldown timelines… ${readyCount} of ${visiblePulls.length} pulls ready.`}
          />
        {/if}
        {#each report.fights as pull (pull.id)}
          {@const summary = manualRankingReport(report, pull)}
          {@const boss = findBoss(pull.encounterID, pull.name)}
          <section
            class="pull-results"
            hidden={String(pull.encounterID) !== bosses[code]}
            aria-label={`${pull.name} fight ${pull.id} from ${code}`}
          >
            <CompRankingsResults
              rankings={{
                reports: [summary],
                page: 1,
                totalCount: 1,
                hasMorePages: false,
                bossSlug: boss?.nameSlug ?? slugifyBossName(pull.name),
                difficulty: String(pull.difficulty),
                updated: summary.startTime
              }}
              {worldData}
              {edition}
              bossIconName={boss?.icon ?? getBossPortraitUrl(slugifyBossName(pull.name))}
              manualMode
              onprogress={(value) => progress.set(fightKey(code, pull.id), value)}
            />
          </section>
        {/each}
      {:else}<p>No boss pulls were found in this report.</p>{/if}
    </section>
  {/each}
</section>

<style lang="scss">
  @use '../lib/styles/selection-tokens' as selectionTokens;

  .report-edition {
    display: grid;
    gap: 16px;
    margin-bottom: 24px;
  }
  .edition-label {
    @include selectionTokens.apply-selection-tokens;
    @include selectionTokens.selection-label;

    margin: 0;
  }
  .reports-page {
    display: grid;
    gap: 1rem;
  }
  p {
    margin: 0;
  }
  .report-meta {
    color: var(--display-muted);
    font-size: 0.85rem;
    line-height: 1.5;
  }
  form,
  .report-selection {
    display: grid;
    gap: 0.7rem;
  }
  .report-selection {
    padding: 0.8rem;
    border: 1px solid var(--display-border);
    background: var(--display-surface);
    border-radius: 0.4rem;
  }
  .report-heading {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.6rem;
    justify-content: space-between;
  }
  .pull-results {
    display: grid;
    gap: 0.7rem;
    min-width: 0;
  }
  .pull-results[hidden] {
    display: none;
  }
</style>
