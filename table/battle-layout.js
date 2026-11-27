// Where the figures stand, in presentation space: never game movement or range.
//
// The same geometry places the Phaser figures and the HTML hit targets over them, so the two cannot drift.
// Order is the board's: allies, then enemies. Every placement keeps its hit target at
// least 44px wide, the smallest a finger reliably lands on.

const MIN_HIT = 44;
// The caption under a figure is taller than the 42px its hit target always kept for it: a name, a row of badges, the
// health bar and the stats. Each figure therefore stands this much higher than where its caption hangs, so the name
// and the badges sit under its feet rather than over its body. A figure only rises into room the stage has.
const CAPTION = 42, LIFT = 34;
const lifted = (one, room) => {
  const lift = Math.max(0, Math.min(LIFT, one.size * .24, room));
  return { ...one, y: one.y - lift, lift, caption: CAPTION + lift };
};

// Whether a stage this size reads as a phone held upright: two rows rather than two opposing formations.
export function isPortrait(width, height) {
  return width < 620 && height > width * .65;
}

export function placements(width, height, allies = 3, enemies = 3, portrait = isPortrait(width, height)) {
  const floor = Math.max(60, height - 44);
  if (portrait) {
    // A hit target is the figure plus its 42px caption, so the rows half a floor apart fit at most
    // that much figure without the lower row's targets covering the upper row's captions. Only a
    // short stage reaches that cap: the one a mutation sheet leaves above itself on a phone.
    const size = Math.max(1, Math.min(width * .30, floor * .40, floor * .50 - 42));
    const row = (y, count, room) => (count === 3 ? [.17, .5, .83] : Array.from({ length: count }, (_, index) => (index + .5) / count))
      .map(x => lifted({ x: width * x, y, size, hitWidth: Math.max(MIN_HIT, width * .29) }, room));
    // Allies rise only into the gap above them, never over the enemies' captions; enemies only into the sky.
    return [...row(floor * .95, allies, floor * .5 - size - CAPTION), ...row(floor * .45, enemies, floor * .45 - size)];
  }
  const size = Math.min(width * .205, floor * .58);
  const coords = [[.36, .90, 1], [.12, .78, .84], [.23, .47, .76], [.64, .90, 1], [.88, .78, .84], [.77, .47, .76]];
  return [...coords.slice(0, allies), ...coords.slice(3, 3 + enemies)]
    .map(([x, y, scale]) => lifted({ x: width * x, y: floor * y, size: size * scale, hitWidth: Math.max(MIN_HIT, size * scale * .80) }, floor * y - size * scale));
}

// Approach along the line between the figures. Portrait opponents stand above each other: a fixed
// horizontal lunge would send the leftmost attacker off-screen. Stop short on that same line instead.
export function strikeApproach(actor, target) {
  const x = target.x - actor.x, y = target.y - actor.y;
  const distance = Math.hypot(x, y);
  if (!distance) return { x: actor.x, y: actor.y, dx: 0, dy: 0 };
  const dx = x / distance, dy = y / distance;
  const gap = Math.min(target.size * .55, distance * .6);
  return { x: target.x - dx * gap, y: target.y - dy * gap, dx, dy };
}

// Where a caster's `Crit!` stands (ADR 0208): beside its head on its open side, never off the top of the field nor
// off either side, the word (`wordsWidth` wide) staying whole on a figure at the field's edge. `type` is its size:
// 13% of the figure, from 14 to 20 px. With the figure measured (`head`: its `rise`, the acting rim's top above its
// feet, and the word's own height), the word's bottom edge stands `CALLOUT_CLEAR` above that rim
// (docs/art/storyboards/movement.md, "A critical's word").
export const CALLOUT_CLEAR = 6;
/**
 * @param {{ x: number, y: number, size: number }} home
 * @param {number} wordsWidth
 * @param {number} fieldWidth
 * @param {{ rise: number, wordsHeight: number } | null} [head]
 */
export function calloutSpot(home, wordsWidth, fieldWidth, head = null) {
  const size = Math.max(28, Math.min(46, home.size * .3));
  const half = wordsWidth / 2 + size * .15;
  const x = Math.min(Math.max(home.x + home.size * .42, half), Math.max(half, fieldWidth - half));
  const y = head ? home.y - head.rise - CALLOUT_CLEAR - head.wordsHeight / 2 : home.y - home.size * .92;
  return { x, y: Math.max(size * .7, y), size, type: Math.max(14, Math.min(20, Math.round(home.size * .13))) };
}
