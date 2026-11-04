// Presentation-only identities. Root mutation IDs stay stable when display names change.
const roots = Object.freeze({ 'tier:brute:v1': 'brute', 'tier:prowler:v1': 'predator', 'tier:occultist:v1': 'warped' });

// ART-5 POC: stage, unique investment, then explicit family order decide the body.
// Purchase/list order, localized labels and shared team passives never choose cosmetics.
const families = ['brute', 'predator', 'warped'];
const branches = [
  ['brute', 'fissures', 'berserker', 'ravager'],
  ['brute', 'plates', 'ironbound', 'dreadnought'],
  ['brute', 'crown', 'marauder', 'warmonger'],
  ['predator', 'talons', 'assassin', 'deathstalker'],
  ['predator', 'fangs', 'parasite', 'soulreaver'],
  ['predator', 'glands', 'plague_doctor', 'blightweaver'],
  ['warped', 'shards', 'elementalist', 'harbinger'],
  ['warped', 'souls', 'necromancer', 'lich'],
  ['warped', 'astral', 'shaman', 'spiritcaller'],
];
const entries = Object.entries(roots).map(([id, family]) => [id, { family, stage: 1, motif: null }]);
for (const [family, motif, second, third] of branches) {
  entries.push([`tier:${second}:v1`, { family, stage: 2, motif }], [`tier:${third}:v1`, { family, stage: 3, motif }]);
}
for (const [index, name] of ['titan', 'apex', 'archmage'].entries()) {
  entries.push([`tier:${name}:v1`, { family: families[index], stage: 4, motif: ['reinforcement', 'precision', 'circuit'][index] }]);
}
export const MUTATION_ART = Object.freeze(Object.fromEntries(entries.map(([id, cue]) => [id, Object.freeze(cue)])));
const compare = (a, b) => b.stage - a.stage || a.id.localeCompare(b.id, 'en');

export function creatureAppearance(creature) {
  const acquired = Array.isArray(creature?.acquiredTiers) ? [...new Set(creature.acquiredTiers)] : [];
  const cues = acquired.filter(id => Object.hasOwn(MUTATION_ART, id))
    .map(id => ({ id, ...MUTATION_ART[id] }));
  // Orphan/unknown later stages cannot invent a family root in a legacy recording.
  const ranked = families.filter(family => cues.some(cue => cue.family === family && cue.stage === 1))
    .map(family => ({ family, owned: cues.filter(cue => cue.family === family) }))
    .map(one => ({ ...one, stage: Math.max(...one.owned.map(cue => cue.stage)) }))
    .sort((a, b) => b.stage - a.stage || b.owned.length - a.owned.length || families.indexOf(a.family) - families.indexOf(b.family));
  const dominant = ranked[0];
  if (!dominant) return { form: 'base-creature', layers: [], description: 'unmutated form' };
  const eligible = ranked.flatMap(one => one.owned);
  const candidates = eligible.filter(cue => cue.stage === 2 || cue.stage === 3).sort(compare);
  const primary = candidates.find(cue => cue.family === dominant.family);
  const secondary = candidates.find(cue => cue.family !== dominant.family)
    ?? candidates.find(cue => cue.motif !== primary?.motif);
  const capstone = eligible.filter(cue => cue.stage === 4).sort((a, b) =>
    Number(b.family === dominant.family) - Number(a.family === dominant.family) || compare(a, b))[0];
  const layers = [[primary, 'body'], [secondary, 'accent'], [capstone, 'capstone']]
    .filter(([cue]) => cue).map(([cue, slot]) => ({ ...cue, slot }));
  return { form: dominant.family, layers,
    description: `${dominant.family} form${layers.map(cue => ` · ${cue.motif} stage ${cue.stage}`).join('')}` };
}
export function creatureForm(creature) { return creatureAppearance(creature).form; }

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
