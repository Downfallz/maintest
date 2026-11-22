import type Phaser from 'phaser';
import { KitContext, type BeatPlay, type Engine, type KitView, type Primitive } from './kit';
import { burst } from './burst';
import { flow } from './flow';
import { trail } from './trail';
import { gather } from './gather';
import { glow } from './glow';
import { flash } from './flash';
import { shake } from './shake';
import { pulse } from './pulse';
import { push } from './push';

export type { Anchor, BeatPlay, StagedBeat } from './kit';

// One small file a primitive. A beat this table does not name is not the kit's: the staging falls back to the motif.
const PRIMITIVES: Record<string, Primitive> = { burst, flow, trail, gather, glow, flash, shake, pulse, push };

export interface EffectsKit {
  /** The beats this kit plays. A staging that names another is drawn as its motif. */
  readonly beats: readonly string[];
  /** Play one beat, after its delay inside its window. Nothing under reduced motion. */
  play(play: BeatPlay): void;
  /** End everything: objects destroyed, tints and filters put back. */
  clear(): void;
  /** What is alive, for the bench and the browser specs. */
  playing(): { beat: string; creature: string }[];
  /** Where each object alive stands, for the bench and the browser specs. */
  places(): { beat: string; creature: string; x: number; y: number; kind: string }[];
  particles(): number;
  changes(): number;
}

/** The kit for one scene. `engine` is the Phaser the caller already holds; `reduced` is read at each beat; `view` is the scene's trauma. */
export function createKit(engine: Engine, scene: Phaser.Scene, reduced: () => boolean, view?: KitView): EffectsKit {
  const context = new KitContext(engine, scene, reduced, view);
  return {
    beats: Object.keys(PRIMITIVES),
    play(play) {
      const primitive = PRIMITIVES[play.beat.beat];
      if (!primitive || context.still) return;
      // Asked again when the delay ends: the setting may have changed while the beat waited.
      context.after(play.beat.delay / play.rate, () => { if (!context.still) primitive(context, play); });
    },
    clear: () => context.clear(),
    playing: () => context.playing(),
    places: () => context.places(),
    particles: () => context.alive(),
    changes: () => context.changes,
  };
}
