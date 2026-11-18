import type Phaser from 'phaser';

/** How long the field stays at its full rate after the last thing that moved. */
export const SETTLE_MS = 2000;
/** At rest the field is drawn one animation frame in this many: fifteen frames a second on a sixty-hertz screen. */
export const REST_EVERY = 4;
// The longest step a resting field takes at once, when the page itself is held up.
const LONGEST_STEP_MS = 250;

/** What a scene tells the loop: whether something on it moves now. */
export interface Restful { busy(): boolean }

// The game loop never stops, but a still field is drawn less often (#842; battle/README.md, "The resting loop").
// At sixty frames a second over a field where only the ambience breathes, a phone pays in battery, and a page with
// a canvas drawn in software has no frame left for the rest of the page. At rest the game is stepped one animation
// frame in `REST_EVERY`: the frames between cost the page nothing, and the ambience goes on, on fewer frames.
// `moving` is what the page itself plays on the loop's clock (the bench's play), which counts as movement too.
export function rest(game: Phaser.Game, scene: Restful, onRest: (resting: boolean) => void, moving: () => boolean = () => false) {
  let quietSince = performance.now();
  let resting = false;
  let skipped = 0;
  let stepped = 0;
  const say = (now: boolean) => { if (resting !== now) { resting = now; onRest(now); } };
  const step = game.step.bind(game);
  game.step = (time: number, delta: number) => {
    const now = performance.now();
    if (scene.busy() || moving()) quietSince = now;
    // A loop its page stopped (the bench's film steps the game by hand) is stepped as asked.
    const atRest = game.loop.running && now - quietSince >= SETTLE_MS;
    say(atRest);
    if (!atRest) skipped = 0;
    else if (++skipped < REST_EVERY) return;
    // A resting step covers the frames it skipped, so the ambience keeps its pace.
    const since = skipped ? Math.min(LONGEST_STEP_MS, time - stepped) : delta;
    skipped = 0;
    stepped = time;
    step(time, since);
  };
  /** Draw at the full rate again, from the next frame: a resized canvas is blank until it is drawn. */
  const wake = () => { quietSince = performance.now(); skipped = REST_EVERY; say(false); };
  return { wake, get resting() { return resting; } };
}
