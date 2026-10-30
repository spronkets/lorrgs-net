<script lang="ts">
  import { getWowheadIconUrl } from '../lib/wowheadIcons';
  import { getClassIconUrl, getRoleIconUrl, getSpecIconUrl } from '../lib/wowAssets';

  export let worldData = {};

  let activeTab = 'classes';

  const tabs = ['classes', 'specs', 'roles', 'bosses', 'zones', 'spells', 'trinkets', 'seasons'];
</script>

<div class="world-data-page">
  <h2>World Data</h2>

  <div class="tabs">
    {#each tabs as tab (tab)}
      <button
        class={['button', ['world-data-page-button', { active: activeTab === tab }]]}
        data-tone="unstyled"
        data-outline="false"
        data-size="medium"
        type="button"
        onclick={() => (activeTab = tab)}
      >
        {tab.charAt(0).toUpperCase() + tab.slice(1)}
      </button>
    {/each}
  </div>

  <div class="tab-content">
    {#if activeTab === 'classes'}
      <div class="data-grid">
        {#each worldData.classes || [] as cls (cls.nameSlug)}
          <div class="data-item">
            <img
              class="entry-icon"
              src={getClassIconUrl(cls.nameSlug)}
              alt={cls.name}
              onerror={(e) => (e.target.style.display = 'none')}
            />
            <div class={`class-badge ${cls.nameSlug}`}></div>
            <div class="data-info">
              <h4 class={`class-name ${cls.nameSlug}`}>
                {cls.name}
              </h4>
              <p class="slug">{cls.nameSlug}</p>
            </div>
          </div>
        {/each}
      </div>
    {:else if activeTab === 'specs'}
      <div class="data-grid">
        {#each worldData.specs || [] as spec (spec.id)}
          <div class="data-item">
            <img
              class="entry-icon"
              src={getSpecIconUrl(
                worldData.classes?.find((c) => c.id === spec.classId)?.nameSlug || 'other',
                spec.nameSlug
              )}
              alt={spec.fullName}
              onerror={(e) => (e.target.style.display = 'none')}
            />
            <div class="spec-info">
              <h4>{spec.fullName}</h4>
              <p class="slug">{spec.fullNameSlug}</p>
              <p
                class={`meta class-name ${worldData.classes?.find((c) => c.id === spec.classId)?.nameSlug ?? ''}`}
              >
                {worldData.classes?.find((c) => c.id === spec.classId)?.name}
              </p>
            </div>
          </div>
        {/each}
      </div>
    {:else if activeTab === 'roles'}
      <div class="data-grid">
        {#each worldData.roles || [] as role (role.id)}
          <div class="data-item">
            <img
              class="entry-icon"
              src={getRoleIconUrl(role.nameSlug)}
              alt={role.name}
              onerror={(e) => (e.target.style.display = 'none')}
            />
            <div class="role-badge" style="background-color: {role.color}"></div>
            <div class="data-info">
              <h4>{role.name}</h4>
              <p class="slug">{role.nameSlug}</p>
              <p class="meta">Metric: {role.metric?.toUpperCase() || 'N/A'}</p>
            </div>
          </div>
        {/each}
      </div>
    {:else if activeTab === 'bosses'}
      <div class="data-grid">
        {#each worldData.bosses || [] as boss (boss.id)}
          <div class="data-item">
            <div class="boss-info">
              <h4>{boss.name}</h4>
              <p class="slug">{boss.nameSlug}</p>
              <p class="meta">ID: {boss.id}</p>
            </div>
          </div>
        {/each}
      </div>
    {:else if activeTab === 'zones'}
      <div class="data-grid">
        {#each worldData.zones || [] as zone (zone.slug)}
          <div class="data-item">
            <div class="zone-info">
              <h4>{zone.name}</h4>
              <p class="slug">{zone.slug}</p>
              <p class="meta">{zone.bossIds?.length || 0} bosses</p>
            </div>
          </div>
        {/each}
      </div>
    {:else if activeTab === 'spells'}
      <div class="data-grid">
        {#each worldData.spells || [] as spell (spell.id)}
          <div class="data-item">
            <img
              class="spell-icon"
              src={getWowheadIconUrl(spell.icon, 'spells')}
              alt={spell.name}
              onerror={(e) => (e.target.style.display = 'none')}
            />
            <div class="data-info">
              <h4>{spell.name}</h4>
              <p class="slug">ID: {spell.id}</p>
              <p class="meta">{spell.type}</p>
            </div>
          </div>
        {/each}
      </div>
    {:else if activeTab === 'trinkets'}
      <div class="data-grid">
        {#each worldData.trinkets || [] as trinket (trinket.id)}
          <div class="data-item">
            <img
              class="spell-icon"
              src={getWowheadIconUrl(trinket.icon, 'trinkets')}
              alt={trinket.name}
              onerror={(e) => (e.target.style.display = 'none')}
            />
            <div class="data-info">
              <h4>{trinket.name}</h4>
              <p class="slug">ID: {trinket.id}</p>
              <p class="meta">iLvl {trinket.itemLevel}</p>
            </div>
          </div>
        {/each}
      </div>
    {:else if activeTab === 'seasons'}
      <div class="data-grid">
        {#each worldData.seasons || [] as season (season.slug)}
          <div class="data-item">
            <div class="data-info">
              <h4>{season.name}</h4>
              <p class="slug">{season.slug}</p>
              <p class="meta">
                Started: {season.startDate
                  ? new Date(season.startDate).toLocaleDateString()
                  : 'N/A'}
              </p>
            </div>
          </div>
        {/each}
      </div>
    {/if}
  </div>
</div>

<style lang="scss">
  @use '../lib/styles/classes';

  .class-name {
    @include classes.text-color;
  }
  .world-data-page {
    animation: fadeIn 0.3s ease-in-out;
  }

  @keyframes fadeIn {
    from {
      opacity: 0;
    }
    to {
      opacity: 1;
    }
  }

  h2 {
    margin: 0 0 1.5rem 0;
    font-size: 1.8rem;
  }

  .tabs {
    display: flex;
    gap: 0.5rem;
    margin-bottom: 2rem;
    border-bottom: 1px solid #333;
  }

  .tabs :global(button.world-data-page-button) {
    background: transparent;
    border: none;
    border-bottom: 2px solid transparent;
    color: #999;
    padding: 0.75rem 1.5rem;
    cursor: pointer;
    font-size: 0.95rem;
    transition: all 0.2s;
  }

  .tabs :global(button.world-data-page-button:hover) {
    color: #ccc;
  }

  .tabs :global(button.active.world-data-page-button) {
    color: #fff;
    border-bottom-color: #666;
  }

  .tab-content {
    animation: fadeIn 0.2s ease-in-out;
  }

  .data-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
    gap: 1rem;
  }

  .data-item {
    background: #1a1a1a;
    border: 1px solid #333;
    border-radius: 0.5rem;
    padding: 1rem;
    display: flex;
    align-items: center;
    gap: 1rem;
    transition: all 0.2s;
  }

  .data-item:hover {
    border-color: #555;
    background: #222;
  }

  .entry-icon {
    width: 44px;
    height: 44px;
    border-radius: 0.4rem;
    border: 1px solid #2d2d2d;
    background: #0f0f0f;
    object-fit: cover;
    flex-shrink: 0;
  }

  .class-badge,
  .role-badge {
    width: 40px;
    height: 40px;
    border-radius: 0.5rem;
    flex-shrink: 0;
    opacity: 0.8;
  }

  .spell-icon {
    width: 36px;
    height: 36px;
    border-radius: 4px;
    flex-shrink: 0;
  }

  .data-info,
  .spec-info,
  .boss-info,
  .zone-info {
    flex: 1;
    min-width: 0;
  }

  h4 {
    margin: 0 0 0.25rem 0;
    font-size: 0.95rem;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .slug {
    margin: 0;
    font-size: 0.8rem;
    color: #666;
    font-family: monospace;
  }

  .meta {
    margin: 0.25rem 0 0 0;
    font-size: 0.8rem;
    color: #888;
  }
</style>
