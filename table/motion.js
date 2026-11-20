// The motion kit's module (ADR 0197, docs/art/ux-craft.md#motion). The kit itself is CSS, in identity.css: this
// brings it what CSS cannot read. The setting for less motion (ADR 0140) becomes one attribute on the root, which
// the kit's rules read beside the system's preference; one ambience plays a screen, in view and in a visible tab;
// and a number that changes plays its beat again.
// It imports nothing, so any page may load it.

/** Where the game settings are kept in this browser (game-preferences.js); read here, never written. */
export const SETTINGS_KEY = 'downfall.arena.game-settings.v1';

/** Whether this browser's settings, the account's as last read, ask for less motion. */
export function savedLess(storage) {
  try { return JSON.parse(storage?.getItem(SETTINGS_KEY) ?? 'null')?.reducedMotion === true; } catch { return false; }
}

/**
 * Whether a page shows less motion beyond what the system asks: the game's own setting on the battle's page,
 * which the match settings change for that game alone, and the account's everywhere else.
 */
export function lessMotion({ saved = false, game } = {}) {
  return game === 'true' || game === 'false' ? game === 'true' : saved;
}

/** Less motion reaches a page as `data-motion="less"` on its root. */
export function reach(root, less) {
  if (less) root.dataset.motion = 'less'; else delete root.dataset.motion;
}

/**
 * Whether a script holds its own animation back: the system asks for less motion, or the page was told to show
 * less (the root's attribute). A rule of CSS reads both; an animation a script starts must ask here.
 */
export function wantsLessMotion(win = globalThis) {
  return win.matchMedia?.('(prefers-reduced-motion: reduce)').matches === true || win.document?.documentElement.dataset.motion === 'less';
}

/** The one ambience a screen plays: the first in the page's order that is in view, none in a hidden tab. */
export function ambienceToPlay(elements, inView, hidden) {
  return hidden ? null : elements.find(element => inView.has(element)) ?? null;
}

/** A number that changed plays its beat, again each time: call it once the new text is set. */
export function numberChanged(element) {
  if (!element) return;
  element.classList.remove('motion-changed');
  // Asking for a layout between the two is what lets the same animation start again.
  element.getBoundingClientRect();
  element.classList.add('motion-changed');
}

/** What plays unasked: a picture that breathes, and a strip of frames (picture.js). */
export const AMBIENCE = '.motion-breath, .motion-strip';

/** Plays ambience only in view and in a visible tab, one a screen. The kit's CSS leaves it still otherwise. */
function watchAmbience(win) {
  const doc = win.document, inView = new Set();
  const elements = () => [...doc.querySelectorAll(AMBIENCE)];
  function play() {
    const playing = ambienceToPlay(elements(), inView, doc.hidden);
    for (const element of elements()) {
      if (element === playing) element.dataset.ambient = 'playing'; else delete element.dataset.ambient;
    }
  }
  if (!win.IntersectionObserver) return () => {};
  const observer = new win.IntersectionObserver(entries => {
    for (const entry of entries) { if (entry.isIntersecting) inView.add(entry.target); else inView.delete(entry.target); }
    play();
  });
  const watch = () => {
    for (const element of elements()) observer.observe(element);
    play();
  };
  doc.addEventListener('visibilitychange', play);
  watch();
  return watch;
}

/** Mounts the kit on a page. Returns `watch`, to call after adding an ambience to the page. */
export function mountMotion(win = globalThis) {
  const doc = win.document, root = doc.documentElement, battle = doc.getElementById('battle');
  const less = () => lessMotion({ saved: savedLess(win.localStorage), game: battle?.dataset.reducedMotion });
  const apply = () => reach(root, less());
  apply();
  // Another tab changed the settings, or this page's settings panel read or saved them.
  win.addEventListener('storage', event => { if (!event.key || event.key === SETTINGS_KEY) apply(); });
  win.addEventListener('downfall:settings', apply);
  if (battle && win.MutationObserver) new win.MutationObserver(apply).observe(battle, { attributes: true, attributeFilter: ['data-reduced-motion'] });
  return { watch: watchAmbience(win) };
}

/**
 * The kit on this page, mounted once whichever loads it first, the page's `<script>` or a module that imports it:
 * a page that adds an ambience after the first paint (the home's featured space) calls `watch()` on it.
 */
export const motion = globalThis.document?.documentElement ? mountMotion() : null;
