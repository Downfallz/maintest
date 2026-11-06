// Where the figures stand, in presentation space: never game movement or range.
//
// The same geometry places the Phaser figures and the HTML hit targets over them, so the two cannot drift.
// Order is the board's: allies, then enemies. Every placement keeps its hit target at
// least 44px wide, the smallest a finger reliably lands on.

const MIN_HIT = 44;

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
    const row = (y, count) => (count === 3 ? [.17, .5, .83] : Array.from({ length: count }, (_, index) => (index + .5) / count)).map(x => ({ x: width * x, y, size, hitWidth: Math.max(MIN_HIT, width * .29) }));
    return [...row(floor * .95, allies), ...row(floor * .45, enemies)];
  }
  const size = Math.min(width * .205, floor * .58);
  const coords = [[.36, .90, 1], [.12, .78, .84], [.23, .47, .76], [.64, .90, 1], [.88, .78, .84], [.77, .47, .76]];
  return [...coords.slice(0, allies), ...coords.slice(3, 3 + enemies)].map(([x, y, scale]) => ({ x: width * x, y: floor * y, size: size * scale, hitWidth: Math.max(MIN_HIT, size * scale * .80) }));
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
