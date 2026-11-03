// Where the six figures stand, in presentation space: never game movement or range.
//
// The same geometry places the Phaser figures and the HTML hit targets over them, so the two cannot drift.
// Order is the board's: the three allies, then the three enemies. Every placement keeps its hit target at
// least 44px wide, the smallest a finger reliably lands on.

const MIN_HIT = 44;

// Whether a stage this size reads as a phone held upright: two rows rather than two opposing formations.
export function isPortrait(width, height) {
  return width < 620 && height > width * .65;
}

export function placements(width, height) {
  const floor = Math.max(60, height - 44);
  if (isPortrait(width, height)) {
    const size = Math.min(width * .30, floor * .40);
    const row = y => [.17, .5, .83].map(x => ({ x: width * x, y, size, hitWidth: Math.max(MIN_HIT, width * .29) }));
    return [...row(floor * .95), ...row(floor * .45)];
  }
  const size = Math.min(width * .205, floor * .58);
  const coords = [[.36, .90, 1], [.12, .78, .84], [.23, .47, .76], [.64, .90, 1], [.88, .78, .84], [.77, .47, .76]];
  return coords.map(([x, y, scale]) => ({ x: width * x, y: floor * y, size: size * scale, hitWidth: Math.max(MIN_HIT, size * scale * .80) }));
}
