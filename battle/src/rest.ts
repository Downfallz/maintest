import Phaser from 'phaser';

/** How long the ambience goes on after the last thing that moved, before the loop sleeps. */
export const SETTLE_MS = 2000;

/** What a scene tells the loop: whether something on it moves now, and a way to wake the loop when it must draw. */
export interface Restful { busy(): boolean; stir: () => void }

// The game loop sleeps when nothing on the field moves (#842; battle/README.md, "The resting loop"). Otherwise Phaser
// draws sixty frames a second over a still field. A phone pays for that in battery, and a page with a canvas drawn
// in software has no frame left for the rest of the page. `moving` is what the page itself plays on the loop's
// clock (the bench's play), which keeps it awake too.
export function rest(game: Phaser.Game, scene: Phaser.Scene & Restful, onRest: (resting: boolean) => void,
  moving: () => boolean = () => false) {
  let quietSince = performance.now();
  let resting = false;
  const awake = () => { if (resting) { resting = false; onRest(false); } };
  const wake = () => {
    quietSince = performance.now();
    if (!resting) return;
    awake();
    // Phaser's wake steps a frame at once. If that happened while a board or a tween was still being added, the
    // scene's update would run on a half-built state, so the step waits until the caller is done. `resetDelta` makes
    // the first step after a sleep one frame long for the scene's own clock (the horizon, the ambience); the tweens'
    // clock caps a long gap by itself. A loop its page stopped (the bench's film, stepped by hand) stays stopped.
    queueMicrotask(() => { if (resting || game.loop.running || !game.loop.started) return; game.loop.resetDelta(); game.loop.wake(); });
  };
  scene.stir = wake;
  // A tween is added from whatever starts it, a board or a file that arrived: each one wakes the loop to play it.
  const tweens = scene.tweens;
  for (const method of ['add', 'addCounter', 'chain', 'addMultiple'] as const) {
    const original = tweens[method] as (...args: unknown[]) => unknown;
    (tweens as unknown as Record<string, unknown>)[method] = (...args: unknown[]) => { wake(); return original.apply(tweens, args); };
  }
  // A page shown again (another tab, or restored from the back-forward cache) draws its field once more, in case the
  // browser dropped the canvas's last frame while it was away.
  const shown = () => { if (!document.hidden) wake(); };
  document.addEventListener('visibilitychange', shown);
  window.addEventListener('pageshow', shown);
  const settle = () => {
    // A loop its page stopped (the bench's film steps the game by hand) is not this one's to put to sleep, and a loop
    // the page started again has woken without our wake call.
    awake();
    if (!game.loop.running) return;
    const now = performance.now();
    if (scene.busy() || moving()) { quietSince = now; return; }
    if (now - quietSince < SETTLE_MS) return;
    // The frame being stepped is still drawn; the loop stops after it, so the canvas keeps the field as it stands.
    resting = true;
    game.loop.sleep();
    onRest(true);
  };
  game.events.on(Phaser.Core.Events.POST_RENDER, settle);
  game.events.once(Phaser.Core.Events.DESTROY, () => {
    game.events.off(Phaser.Core.Events.POST_RENDER, settle);
    document.removeEventListener('visibilitychange', shown);
    window.removeEventListener('pageshow', shown);
  });
  return { wake, get resting() { return resting; } };
}
