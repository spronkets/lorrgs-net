<script lang="ts">
  import { onMount } from 'svelte';
  import type { WorldDataSnapshot } from '../lib/models/WorldDataSnapshot';
  import type { RaidCatalog } from '../lib/models/RaidCatalog';
  import EncounterFilters from '../lib/components/EncounterFilters.svelte';

  export let worldData: WorldDataSnapshot = {};
  export let raidCatalog: RaidCatalog = { editions: [], instances: {} };
  export let selectedVersion = '';
  let selectedRaid = '';
  let selectedBoss = '';
  let hydrated = false;
  export let oneditionchange: ((_version: string) => void) | undefined = undefined;
  export let onnavigate: ((_mode: 'spec' | 'comp') => void) | undefined = undefined;

  onMount(() => {
    const params = new URLSearchParams(window.location.search);
    selectedRaid = params.get('raid') ?? '';
    selectedBoss = params.get('boss') ?? '';
    hydrated = true;
  });

  function syncSelection(version: string, raid: string, boss: string) {
    const url = new URL(window.location.href);
    for (const [key, value] of [
      ['edition', version],
      ['raid', raid],
      ['boss', boss]
    ]) {
      if (value) url.searchParams.set(key, value);
      else url.searchParams.delete(key);
    }
    window.history.replaceState({}, '', `${url.pathname}${url.search}${url.hash}`);
  }

  $: if (hydrated) syncSelection(selectedVersion, selectedRaid, selectedBoss);
</script>

<section class="landing-page">
  <EncounterFilters
    {worldData}
    {raidCatalog}
    bind:selectedVersion
    bind:selectedRaid
    bind:selectedBoss
    {oneditionchange}
  />
  <div class="ranking-flows">
    <button
      class={['button', ['landing-page-button']]}
      data-tone="primary"
      data-outline="true"
      data-size="small"
      type="button"
      disabled={!selectedBoss}
      onclick={() => onnavigate?.('spec')}>Spec Rankings</button
    >
    <button
      class={['button', ['landing-page-button']]}
      data-tone="primary"
      data-outline="true"
      data-size="small"
      type="button"
      disabled={!selectedBoss}
      onclick={() => onnavigate?.('comp')}>Comp Rankings</button
    >
  </div>
  <section class="about" aria-labelledby="about-heading">
    <h2 id="about-heading">What is this?</h2>
    <p>
      LorrgsNET is a personal, unofficial adaptation of
      <a href="https://lorrgs.io/" target="_blank" rel="noopener noreferrer">Lorrgs</a>, created by
      porting its Python API to .NET and rebuilding the frontend in Svelte. It provides shared tools
      for exploring specializations, raid compositions, and individual reports. Compare important
      ability casts on encounter timelines to understand how cooldowns align with boss mechanics and
      phase transitions.
    </p>
    <p>
      <strong>Spec Rankings</strong> — Compare cooldown usage across ranked players of a chosen specialization.
    </p>
    <p>
      <strong>Comp Rankings</strong> — Compare raid-wide cooldown usage across execution-ranked kills
      that match your composition criteria.
    </p>
  </section>
  <aside class="disclaimer" aria-labelledby="disclaimer-heading">
    <h2 id="disclaimer-heading">Disclaimer: use timings as a reference</h2>
    <p>
      A successful cast sequence is not a universal cooldown plan. Adjust it for your raid’s
      composition, strategy, damage output, and phase transitions.
    </p>
    <p>
      Look for the reason behind a timing: which mechanic it covers, what other cooldowns support
      it, and whether your group reaches that point under comparable conditions.
    </p>
    <p class="example">
      For example, a healing plan from a four-healer kill may only fit your raid if you use a
      similar setup and reach the next phase at a similar time.
    </p>
  </aside>
</section>

<style>
  .landing-page {
    width: 100%;
    max-width: 1600px;
    margin: 0 auto;
  }
  .ranking-flows {
    display: flex;
    flex-wrap: wrap;
    gap: 16px;
  }

  .about,
  .disclaimer {
    margin-top: 32px;
    line-height: 1.65;
  }
  h2 {
    font-size: 1.2rem;
    line-height: 1.3;
  }
  .disclaimer {
    border-left: 3px solid #b98526;
    padding-left: 20px;
  }
  .example {
    font-style: italic;
  }
</style>
