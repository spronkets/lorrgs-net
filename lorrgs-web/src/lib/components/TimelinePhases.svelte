<script lang="ts">
  import { timelineTime } from '../timelineUtils';
  import type { TimelinePhase } from '../api';
  type Props = { phases: TimelinePhase[]; axisDuration: number };
  let { phases, axisDuration }: Props = $props();
</script>

{#each phases as phase, index (index)}
  <span
    class="phase"
    style={`left: ${(phase.timestamp / axisDuration) * 100}%`}
    role="img"
    aria-label={`${phase.isIntermission ? 'Intermission' : 'Phase'} ${phase.id} at ${timelineTime(phase.timestamp)}`}
    title={`${phase.isIntermission ? 'Intermission' : 'Phase'} ${phase.id} · ${timelineTime(phase.timestamp)}`}
    ><span>{phase.isIntermission ? 'I' : 'P'}{phase.id}</span></span
  >
{/each}

<style>
  .phase {
    position: absolute;
    top: 0;
    bottom: 0;
    border-left: 1px solid #54bb68;
    color: #54bb68;
    font: 10px monospace;
    z-index: 0;
    pointer-events: none;
  }
  .phase span {
    padding-left: 2px;
    background: var(--panel);
  }
</style>
