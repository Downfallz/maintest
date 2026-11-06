// The three bodies, in the order a tie between them is broken (ADR 0133). Which package belongs to which body,
// and what it draws on it, comes with the catalogue's packages: level and visual, published beside the content.
// The page names no package; a package nobody drew, and an older host that serves none, leave no trace.
const FAMILIES = ['brute', 'predator', 'warped'];
const BODIES = { brute: 'Brute', predator: 'Predator', warped: 'Warped' };
const compare = (a, b) => b.stage - a.stage || a.id.localeCompare(b.id, 'en');

function acquiredCues(creature, packages) {
  const acquired = Array.isArray(creature?.acquiredTiers) ? [...new Set(creature.acquiredTiers)] : [];
  return acquired.map(id => ({ id, pack: packages?.get(id) }))
    .filter(({ pack }) => FAMILIES.includes(pack?.visual?.family) && Number.isInteger(pack.level) && pack.level >= 1)
    .map(({ id, pack }) => ({ id, name: pack.name ?? id, family: pack.visual.family, stage: pack.level, motif: pack.visual.motif ?? null }));
}

// ART-5 POC: the body is the family with the highest stage bought, then the most packages, then the fixed order.
// Purchase order, localized labels and shared team passives never choose cosmetics.
export function creatureAppearance(creature, packages) {
  const cues = acquiredCues(creature, packages);
  // An orphan later stage cannot invent a root in a legacy recording: a family needs its root to be a body.
  const ranked = FAMILIES.filter(family => cues.some(cue => cue.family === family && cue.stage === 1))
    .map(family => ({ family, owned: cues.filter(cue => cue.family === family) }))
    .map(one => ({ ...one, stage: Math.max(...one.owned.map(cue => cue.stage)) }))
    .sort((a, b) => b.stage - a.stage || b.owned.length - a.owned.length || FAMILIES.indexOf(a.family) - FAMILIES.indexOf(b.family));
  const dominant = ranked[0];
  if (!dominant) return { form: 'base-creature', layers: [], description: 'Unmutated' };
  const eligible = ranked.flatMap(one => one.owned);
  const candidates = eligible.filter(cue => cue.stage === 2 || cue.stage === 3).sort(compare);
  const primary = candidates.find(cue => cue.family === dominant.family);
  const secondary = candidates.find(cue => cue.family !== dominant.family)
    ?? candidates.find(cue => cue.motif !== primary?.motif);
  const capstone = eligible.filter(cue => cue.stage >= 4).sort((a, b) =>
    Number(b.family === dominant.family) - Number(a.family === dominant.family) || compare(a, b))[0];
  const layers = [[primary, 'body'], [secondary, 'accent'], [capstone, 'capstone']]
    .filter(([cue]) => cue?.motif).map(([cue, slot]) => ({ ...cue, slot }));
  // Said in the catalogue's words, the package names a player bought, never the renderer's motif names.
  return { form: dominant.family, layers,
    description: `${BODIES[dominant.family]} body${layers.map(cue => ` · ${cue.name} (stage ${cue.stage})`).join('')}` };
}
export function creatureForm(creature, packages) { return creatureAppearance(creature, packages).form; }

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
