<script lang="ts">
  import { onMount } from 'svelte';
  import type { RankingReport, ReportTimeline } from '../api';
  import { buildRaidNote, specNoteTag, type NoteFormat, type NoteTarget } from '../raidNote';

  type Props = {
    timeline: ReportTimeline;
    report: RankingReport;
    hidden: Set<number>;
    difficulty: string;
    bossName: string;
    specSlug: string;
    onclose: () => void;
    oncopied: (_name: string) => void;
  };
  let { timeline, report, hidden, difficulty, bossName, specSlug, onclose, oncopied }: Props =
    $props();
  let dialog: HTMLDialogElement;
  let format = $state<NoteFormat>('mrt');
  let target = $state<NoteTarget>('name');
  let playerName = $state('');
  let dynamic = $state(false);
  let copyError = $state('');
  const fight = $derived(report.fights?.find((fight) => fight.fightId === timeline.fightId));
  const hasPhases = $derived(
    (
      fight?.phases ??
      (report.bossTimeline?.fightId === timeline.fightId ? report.bossTimeline.phases : [])
    ).some((phase) => phase.timestamp > 0)
  );
  const specId = $derived(
    report.players?.find((player) => player.specSlug === specSlug)?.specId ??
      report.players?.find((player) => player.name === timeline.playerName)?.specId ??
      0
  );
  const note = $derived(
    buildRaidNote(timeline, report, hidden, {
      format,
      target,
      playerName,
      dynamic: hasPhases && dynamic,
      difficulty,
      bossName,
      specSlug
    })
  );
  onMount(() => {
    dialog.showModal();
    return () => dialog.close();
  });

  async function copy() {
    copyError = '';
    try {
      await navigator.clipboard.writeText(note.text);
      oncopied(timeline.playerName);
    } catch (error) {
      console.error('Unable to copy raid note', error);
      copyError =
        'Unable to copy note. Select the preview and copy it manually, or check clipboard permissions.';
    }
  }
</script>

<dialog bind:this={dialog} aria-labelledby="copy-note-title" {onclose}>
  <header>
    <h2 id="copy-note-title">Copy Note</h2>
    <button type="button" aria-label="Close copy note" onclick={() => dialog.close()}>Close</button>
  </header>
  <p class="source">{timeline.playerName} · {bossName || fight?.name || 'Boss'}</p>
  <div class="options">
    <label
      >Target
      <select bind:value={target}>
        <option value="name">Player Name</option>
        <option value="spec">Player Spec</option>
      </select>
    </label>
    {#if target === 'name'}
      <label
        >Name or Nickname
        <input bind:value={playerName} placeholder={timeline.playerName} />
      </label>
    {:else}
      <label
        >Spec tag
        <input readonly value={specNoteTag(specId, format) ?? 'Unavailable'} />
      </label>
    {/if}
    <fieldset>
      <legend>Note format</legend>
      <button type="button" aria-pressed={format === 'mrt'} onclick={() => (format = 'mrt')}
        >MRT</button
      >
      <button type="button" aria-pressed={format === 'nsrt'} onclick={() => (format = 'nsrt')}
        >NSRT</button
      >
    </fieldset>
    <label class="dynamic">
      <input type="checkbox" bind:checked={dynamic} disabled={!hasPhases} />
      Phase-relative timers
    </label>
  </div>
  <p class="hint">MRT: Method Raid Tools / Smart CD Reminder. NSRT: Northern Sky Raid Tools.</p>
  {#if !hasPhases}
    <p class="hint">No observed phase transitions; times are relative to pull.</p>
  {:else if dynamic}
    <p class="hint">
      Phase numbering must match the phase notifications used by your raid addon or boss mod.
    </p>
  {/if}
  {#if !timeline.isComplete}
    <p class="warning" role="status">Partial timeline: this note may be missing casts.</p>
  {/if}
  <label class="preview"
    >Note preview
    <textarea
      readonly
      value={note.text}
      spellcheck="false"
      onclick={(event) => event.currentTarget.select()}></textarea>
  </label>
  {#if note.error}<p role="alert">{note.error}</p>{/if}
  {#if copyError}<p role="alert">{copyError}</p>{/if}
  <footer>
    <span>{note.count} selected casts</span>
    <button type="button" disabled={Boolean(note.error)} onclick={copy}>Copy to clipboard</button>
  </footer>
  <p class="hint">Only casts for spells currently shown in the timeline are included.</p>
  <p class="warning">
    Adjust timings for your group's composition and kill time; another player's timings may not suit
    your fight.
  </p>
</dialog>

<style>
  dialog {
    width: min(760px, calc(100vw - 32px));
    max-height: calc(100vh - 48px);
    overflow: auto;
    box-sizing: border-box;
    padding: 18px;
    border: 1px solid var(--line, #bdc9d4);
    border-radius: 6px;
    color: var(--text, #182c40);
    background: var(--panel, #f4f6f8);
    box-shadow: 0 16px 60px #0006;
  }
  dialog::backdrop {
    background: #0009;
  }
  header,
  footer {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
  }
  h2 {
    margin: 0;
    font-size: 20px;
  }
  .source,
  .hint {
    color: var(--muted, #536477);
    font-size: 12px;
  }
  .options {
    display: flex;
    flex-wrap: wrap;
    align-items: end;
    gap: 12px;
  }
  label {
    display: flex;
    flex-direction: column;
    gap: 5px;
    font-size: 12px;
  }
  .dynamic {
    flex-direction: row;
    align-items: center;
    padding-bottom: 6px;
  }
  fieldset {
    margin: 0;
    padding: 4px 8px;
    border: 1px solid var(--line);
    border-radius: 4px;
  }
  legend {
    font-size: 12px;
  }
  button,
  input,
  select,
  textarea {
    font: inherit;
    color: inherit;
    border: 1px solid var(--line, #bdc9d4);
    border-radius: 3px;
    background: var(--panel);
    padding: 6px 8px;
  }
  button {
    cursor: pointer;
  }
  button[aria-pressed='true'] {
    border-color: var(--accent);
    box-shadow: inset 0 -2px var(--accent);
  }
  button:disabled {
    opacity: 0.5;
    cursor: default;
  }
  button:focus-visible,
  input:focus-visible,
  select:focus-visible,
  textarea:focus-visible {
    outline: 2px solid var(--accent);
    outline-offset: 2px;
  }
  .preview {
    margin: 14px 0;
  }
  textarea {
    min-height: 260px;
    resize: vertical;
    font: 12px/1.6 monospace;
  }
  .warning {
    color: #b77821;
    font-size: 12px;
  }
  [role='alert'] {
    color: #ce4141;
    font-size: 13px;
  }
</style>
