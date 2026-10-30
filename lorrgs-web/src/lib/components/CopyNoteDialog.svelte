<script lang="ts">
  import Alert from './Alert.svelte';
  import { onMount } from 'svelte';
  import type { RankingReport } from '../models/RankingReport';
  import type { ReportTimeline } from '../models/ReportTimeline';
  import { buildRaidNote, specNoteTag } from '../raidNote';
  import type { NoteFormat } from '../models/NoteFormat';
  import type { NoteTarget } from '../models/NoteTarget';

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
      (report.bossTimeline?.fightId === timeline.fightId ? report.bossTimeline.phases : null) ??
      fight?.phases ??
      []
    ).some(
      (phase) =>
        phase.timestamp > 0 &&
        !phase.isIntermission &&
        Number.isInteger(phase.notePhaseId ?? phase.id)
    )
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
    } catch (error: unknown) {
      console.error('Unable to copy raid note', error);
      copyError =
        'Unable to copy note. Select the preview and copy it manually, or check clipboard permissions.';
    }
  }
</script>

<dialog bind:this={dialog} aria-labelledby="copy-note-title" {onclose}>
  <header>
    <h2 id="copy-note-title">Copy Note</h2>
    <button
      class={['button', ['copy-note-dialog-button']]}
      data-tone="secondary"
      data-outline="true"
      data-size="small"
      type="button"
      aria-label="Close copy note"
      onclick={() => dialog.close()}>Close</button
    >
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
      <div class="form-field">
        <label for="copynotedialog-field-1">Name or Nickname</label>
        <div class="input-frame">
          <input
            id="copynotedialog-field-1"
            class="input"
            type="text"
            bind:value={playerName}
            placeholder={timeline.playerName}
          />
        </div>
      </div>
    {:else}
      <div class="form-field">
        <label for="copynotedialog-field-2">Spec tag</label>
        <div class="input-frame">
          <input
            id="copynotedialog-field-2"
            class="input"
            type="text"
            readonly
            value={specNoteTag(specId, format) ?? 'Unavailable'}
          />
        </div>
      </div>
    {/if}
    <fieldset>
      <legend>Note format</legend>
      <button
        class={['button', ['copy-note-dialog-button']]}
        data-tone={(format === 'mrt') === true || (format === 'mrt') === 'true'
          ? 'primary'
          : 'secondary'}
        data-outline={!((format === 'mrt') === true || (format === 'mrt') === 'true')}
        data-size="small"
        type="button"
        aria-pressed={format === 'mrt'}
        onclick={() => (format = 'mrt')}>MRT</button
      >
      <button
        class={['button', ['copy-note-dialog-button']]}
        data-tone={(format === 'nsrt') === true || (format === 'nsrt') === 'true'
          ? 'primary'
          : 'secondary'}
        data-outline={!((format === 'nsrt') === true || (format === 'nsrt') === 'true')}
        data-size="small"
        type="button"
        aria-pressed={format === 'nsrt'}
        onclick={() => (format = 'nsrt')}>NSRT</button
      >
    </fieldset>
    <label class="dynamic">
      <input type="checkbox" bind:checked={dynamic} disabled={!hasPhases} />
      Phase-relative timers
    </label>
  </div>
  <p class="hint">MRT: Method Raid Tools / Smart CD Reminder. NSRT: Northern Sky Raid Tools.</p>
  {#if !hasPhases}
    <Alert compact>No observed phase transitions; times are relative to pull.</Alert>
  {:else if dynamic}
    <p class="hint">
      Phase numbering must match the phase notifications used by your raid addon or boss mod.
      Intermission markers are excluded; corrected phase labels retain their observed WCL timer
      anchors.
    </p>
  {/if}
  {#if !timeline.isComplete}
    <Alert variant="warning" compact>Partial timeline: this note may be missing casts.</Alert>
  {/if}
  <div class="preview">
    <div class="form-field">
      <label for="copynotedialog-field-3">Note preview</label><textarea
        id="copynotedialog-field-3"
        class="textarea monospace"
        style="min-height: 260px"
        rows="12"
        readonly
        value={note.text}
        spellcheck="false"
        onclick={(event) => event.currentTarget.select()}></textarea>
    </div>
  </div>
  {#if note.error}<Alert variant="error">{note.error}</Alert>{/if}
  {#if copyError}<Alert variant="error">{copyError}</Alert>{/if}
  <footer>
    <span>{note.count} selected casts</span>
    <button
      class={['button', ['copy-note-dialog-button']]}
      data-tone="primary"
      data-outline="false"
      data-size="small"
      type="button"
      disabled={Boolean(note.error)}
      onclick={copy}>Copy to clipboard</button
    >
  </footer>
  <p class="hint">Only casts for spells currently shown in the timeline are included.</p>
  <Alert variant="warning" compact>
    Adjust timings for your group's composition and kill time; another player's timings may not suit
    your fight.
  </Alert>
</dialog>

<style lang="scss">
  @use '../styles/selection-tokens' as selectionTokens;

  dialog {
    @include selectionTokens.apply-selection-tokens;

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

  select {
    @include selectionTokens.selection-dropdown;
  }

  .preview {
    margin: 14px 0;
  }
</style>
