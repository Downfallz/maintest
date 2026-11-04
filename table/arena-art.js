// Presentation-only identities. Root mutation IDs stay stable when display names change.
const roots = Object.freeze({ 'tier:brute:v1': 'brute', 'tier:prowler:v1': 'predator', 'tier:occultist:v1': 'warped' });

// No multiclass precedence is implied: ART-5 will decide how acquired families compose.
// Missing/legacy mutation data also uses the neutral ancestor, never position or creature name.
export function creatureForm(creature) {
  const families = new Set((creature?.acquiredTiers ?? []).map(id => roots[id]).filter(Boolean));
  return families.size === 1 ? [...families][0] : 'base-creature';
}

const sprite = (form, bounds, ground) => Object.freeze({
  form, key: `creature-${form}`, file: `${form}.webp`, width: 1536, height: 1024,
  bounds: Object.freeze(bounds), ground: Object.freeze(ground),
});

// The ART-2 reference bytes are reused intact. Bounds (alpha >= 128) fit the principal silhouette;
// faint fringe pixels are retained. Ground is a reviewed centre/contact point in source pixels.
export const CREATURE_ART = Object.freeze([
  sprite('base-creature', [139, 47, 1368, 999], [753.5, 999]),
  sprite('brute', [78, 12, 1454, 983], [766, 983]),
  sprite('predator', [61, 158, 1477, 865], [769, 865]),
  sprite('warped', [183, 24, 1371, 1000], [777, 1000]),
]);
const fallback = Object.freeze({ form: 'fallback', key: 'missing-figure', file: '', width: 80, height: 100,
  bounds: Object.freeze([0, 0, 80, 90]), ground: Object.freeze([40, 90]) });

export function figureArt(form, available) {
  const requested = CREATURE_ART.find(asset => asset.form === form) ?? CREATURE_ART[0];
  if (available(requested.key)) return requested;
  return available(CREATURE_ART[0].key) ? CREATURE_ART[0] : fallback;
}

// One scale for both axes: square placement slots must never stretch these quadrupeds.
// Phaser mirrors about the origin, so mirror the source anchor too to keep feet centred on the ring.
export function figureGeometry(asset, home, enemy) {
  const [left, top, right, bottom] = asset.bounds;
  const scale = Math.min(home.hitWidth / (right - left), home.size / (bottom - top));
  return { scale, originX: enemy ? 1 - asset.ground[0] / asset.width : asset.ground[0] / asset.width,
    originY: asset.ground[1] / asset.height };
}
