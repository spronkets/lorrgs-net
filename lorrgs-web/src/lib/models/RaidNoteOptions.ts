import type { NoteFormat } from './NoteFormat';
import type { NoteTarget } from './NoteTarget';

export type RaidNoteOptions = {
  format: NoteFormat;
  target: NoteTarget;
  playerName: string;
  dynamic: boolean;
  difficulty: string;
  bossName: string;
  specSlug: string;
};
