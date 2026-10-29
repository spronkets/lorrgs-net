<script lang="ts">
  import type { WorldDataSnapshot } from '../models/WorldDataSnapshot';
  import { getRaidBossOptions, getRaidsForEdition, groupRaidsByPhase } from '../raidCatalog';
  import type { RaidCatalog } from '../models/RaidCatalog';
  import { getVersionIconUrl } from '../selectionIcons';
  import EditionSelector from './EditionSelector.svelte';
  import RaidBossSelectors from './RaidBossSelectors.svelte';

  export let worldData: WorldDataSnapshot = {};
  export let raidCatalog: RaidCatalog = { editions: [], instances: {} };
  export let selectedVersion = '';
  export let selectedRaid = '';
  export let selectedBoss = '';
  const versions = ['Anniversary', 'Mists of Pandaria', 'Era', 'Retail'];
  export let oneditionchange: ((_version: string) => void) | undefined = undefined;
  $: raids = getRaidsForEdition(raidCatalog, selectedVersion);
  $: raid = raids.find((item) => item.slug === selectedRaid);
  $: bosses = getRaidBossOptions(raid).map((boss) => ({
    ...boss,
    iconName: worldData.bosses?.find((entry) => entry.nameSlug === boss.slug)?.icon
  }));

  function selectVersion(version: string) {
    if (version === selectedVersion) return;
    selectedVersion = version;
    selectedRaid = '';
    selectedBoss = '';
    oneditionchange?.(version);
  }
</script>

<section class="encounter-filters" aria-label="Encounter selection">
  <h3>Edition</h3>
  <EditionSelector {versions} {selectedVersion} {getVersionIconUrl} onselect={selectVersion} />
  <RaidBossSelectors
    raidPhaseGroups={groupRaidsByPhase(raids)}
    {bosses}
    bind:selectedRaid
    bind:selectedBoss
    raidPlaceholder="— choose a raid —"
    bossPlaceholder="— choose a boss —"
  />
</section>

<style lang="scss">
  @use '../styles/selection-tokens' as selectionTokens;

  .encounter-filters {
    @include selectionTokens.apply-selection-tokens;

    --meta-font-size: 1rem;

    display: grid;
    gap: 16px;
    margin-bottom: 24px;
  }
  h3 {
    margin: 0;
    font-size: 1rem;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.08em;
  }
  .encounter-filters :global(label > span) {
    font-size: 1rem;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.08em;
  }
</style>
