import Phaser from 'phaser';

/** How long the field goes on breathing after the last thing that moved, before the loop sleeps. */
export const SETTLE_MS = 2000;

/** What a scene tells the loop: whether something on it moves now, and a way to wake the loop when it must draw. */
export interface Restful { busy(): boolean; stir: () => void }

// The game loop sleeps when nothing on the field moves (#842). Phaser draws sixty frames a second over a still field
// otherwise: a phone pays for it in battery, and a starved page (a canvas drawn in software) has no frame left for
// the page around it. Busy is a tween, a camera effect, the effects kit's particles, a tumbling die, the horizon
// giving way or a file loading; the halos' breath, the motes and the auras are only ambience, and stop with the
// loop until the next board, tween or input wakes it.
// `moving` is what the page itself plays on the loop's clock (the bench's play), which keeps it awake too.
export function rest(game: Phaser.Game, scene: Phaser.Scene & Restful, mount: HTMLElement, onRest: (resting: boolean) => void,
  moving: () => boolean = () => false) {
  let quietSince = performance.now();
  let resting = false;
  const wake = () => {
    quietSince = performance.now();
    if (!resting) return;
    resting = false;
    onRest(false);
    // Phaser's wake steps a frame at once. Woken from inside a board or a tween being added, that frame would run the
    // scene's update halfway through them: it is stepped once the caller is done. A long sleep is not a long frame,
    // so the first step after it moves the tweens it brings by one frame, not by the whole sleep.
    // A loop its page stopped (the bench's film, stepped by hand) stays stopped.
    queueMicrotask(() => { if (resting || game.loop.running || !game.loop.started) return; game.loop.resetDelta(); game.loop.wake(); });
  };
  scene.stir = wake;
  // A tween is added from whatever starts it, a board or a file that arrived: each one wakes the loop to play it.
  const tweens = scene.tweens;
  for (const method of ['add', 'addCounter', 'chain', 'addMultiple'] as const) {
    const original = tweens[method] as (...args: unknown[]) => unknown;
    (tweens as unknown as Record<string, unknown>)[method] = (...args: unknown[]) => { wake(); return original.apply(tweens, args); };
  }
  const touched = () => wake();
  for (const kind of ['pointerdown', 'keydown', 'wheel'] as const) mount.addEventListener(kind, touched, { passive: true });
  const settle = () => {
    // A loop stopped by its page (the bench's film steps the game by hand) is not this one's to put to sleep.
    if (!game.loop.running) return;
    // A loop its page started again after stopping it has woken without us.
    if (resting) { resting = false; onRest(false); }
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
    for (const kind of ['pointerdown', 'keydown', 'wheel'] as const) mount.removeEventListener(kind, touched);
  });
  return { wake, get resting() { return resting; } };
}
