// The evolution illustrations, separate from the four eagerly loaded combat sprites (battle-art.js), and the
// capstone effects. Keys are content IDs rather than display labels, so renaming a package never changes an asset
// path, and nothing here is read off a filename: an entry says which tier it draws, and that is all it says.
// Hybrid images are registered one approved pair at a time, never as the power set of mutations.
const directory = 'evolutions/';

// Each image's silhouette: the box its opaque pixels (alpha >= 128) fill, measured from the file, and the point it
// stands on, the box's horizontal centre on its bottom edge, as the audited sprites use. Bounds, not canvases, set
// the drawn size, so a Stage 3 drawn on a busier canvas is never smaller than the Stage 2 before it.
const entries = [
  // id, family, stage, tier, [width, height], [left, top, right, bottom]
  ['base-creature', 'base', 0, null, [1536, 1024], [139, 47, 1368, 999]],
  ['brute-base', 'brute', 1, 'tier:brute:v1', [1536, 1024], [74, 10, 1457, 987]],
  ['predator-base', 'predator', 1, 'tier:prowler:v1', [1774, 887], [10, 9, 1767, 880]],
  ['warped-base', 'warped', 1, 'tier:occultist:v1', [1536, 1024], [179, 22, 1372, 1002]],
  ['brute-stage2-oppressor', 'brute', 2, 'tier:marauder:v1', [1536, 1024], [24, 7, 1515, 1010]],
  ['brute-stage2-frenzied', 'brute', 2, 'tier:berserker:v1', [1536, 1024], [13, 8, 1531, 1022]],
  ['brute-stage2-ironhide', 'brute', 2, 'tier:ironbound:v1', [1536, 1024], [108, 89, 1450, 953]],
  ['predator-stage2-scythe', 'predator', 2, 'tier:assassin:v1', [1774, 887], [10, 3, 1764, 886]],
  ['predator-stage2-blighted', 'predator', 2, 'tier:plague_doctor:v1', [1536, 1024], [8, 1, 1529, 1024]],
  ['predator-stage2-open-maw', 'predator', 2, 'tier:parasite:v1', [1671, 941], [5, 3, 1666, 938]],
  ['warped-stage2-stormborn', 'warped', 2, 'tier:elementalist:v1', [1536, 1024], [13, 8, 1500, 1014]],
  ['warped-stage2-necrotic', 'warped', 2, 'tier:necromancer:v1', [1536, 1024], [26, 3, 1440, 1021]],
  ['warped-stage2-ethereal', 'warped', 2, 'tier:shaman:v1', [1536, 1024], [22, 8, 1488, 1017]],
  ['brute-stage3-oppressor', 'brute', 3, 'tier:warmonger:v1', [1536, 1024], [16, 5, 1525, 1017]],
  ['brute-stage3-colossus', 'brute', 3, 'tier:dreadnought:v1', [1536, 1024], [39, 19, 1495, 1009]],
  ['brute-stage3-ravager', 'brute', 3, 'tier:ravager:v1', [1536, 1024], [7, 4, 1536, 1024]],
  ['predator-stage3-soulreaver', 'predator', 3, 'tier:soulreaver:v1', [1536, 1024], [7, 2, 1531, 1019]],
  ['predator-stage3-blightweaver', 'predator', 3, 'tier:blightweaver:v1', [1536, 1024], [7, 4, 1532, 1024]],
  ['predator-stage3-deathstalker', 'predator', 3, 'tier:deathstalker:v1', [1536, 1024], [15, 8, 1525, 1022]],
  ['warped-stage3-cataclysm', 'warped', 3, 'tier:harbinger:v1', [1536, 1024], [10, 6, 1529, 1018]],
  ['warped-stage3-revenant', 'warped', 3, 'tier:lich:v1', [1536, 1024], [14, 8, 1523, 1021]],
  ['warped-stage3-transcendent', 'warped', 3, 'tier:spiritcaller:v1', [1536, 1024], [18, 4, 1519, 1020]],
];

const asset = ({ id, family, stage, tier, dominantTierId = null, hybridTierId = null, size, bounds, file = `${directory}${id}.webp` }) => {
  const [left, , right, bottom] = bounds;
  return Object.freeze({ id, family, stage, tier, dominantTierId, hybridTierId, key: `evolution-${id}`, file,
    width: size[0], height: size[1], bounds: Object.freeze([...bounds]), ground: Object.freeze([(left + right) / 2, bottom]) });
};

export const EVOLUTION_ART = Object.freeze(entries.map(([id, family, stage, tier, size, bounds]) => asset({ id, family, stage, tier, size, bounds })));

export const BASE_EVOLUTION_ART = EVOLUTION_ART.find(one => one.stage === 0);

// Approved hybrid illustrations: a Stage 3's own image reworked with another mutation's characteristics. Adding one
// is one entry, its identity, file and silhouette; the resolver (battle-evolution.js) is never edited for it.
// Example of an entry, once its art is approved and committed under table/battle-art/evolutions/hybrids/:
//   { id: 'transcendent-necrotic', dominantTierId: 'tier:spiritcaller:v1', hybridTierId: 'tier:necromancer:v1',
//     file: 'evolutions/hybrids/transcendent-necrotic.webp', size: [1536, 1024], bounds: [...] }
const hybrids = [];

export const HYBRID_ART = Object.freeze(hybrids.map(one => asset({ ...one, family: 'hybrid', stage: 3, tier: null })));

const byTier = new Map(EVOLUTION_ART.filter(one => one.tier).map(one => [one.tier, one]));
const byId = new Map([...EVOLUTION_ART, ...HYBRID_ART].map(one => [one.id, one]));
const pairKey = (dominant, hybrid) => `${dominant}+${hybrid}`;

// A registry: the base image a tier draws, the approved hybrid of a pair, or an image by its stable id. A pair
// nobody drew is null; only the resolver decides what shows in its place.
export function createEvolutionRegistry(hybridArt = HYBRID_ART) {
  const pairs = new Map(hybridArt.map(one => [pairKey(one.dominantTierId, one.hybridTierId), one]));
  const ids = new Map([...byId, ...hybridArt.map(one => [one.id, one])]);
  return {
    tier: tierId => byTier.get(tierId) ?? null,
    hybrid: (dominantTierId, hybridTierId) => pairs.get(pairKey(dominantTierId, hybridTierId)) ?? null,
    art: id => ids.get(id) ?? null,
    base: () => BASE_EVOLUTION_ART,
  };
}

export const EVOLUTION_REGISTRY = createEvolutionRegistry();

// The Stage 3's own image, or an approved hybrid's when the pair was drawn. Kept for the manifest's first callers.
export function evolutionArt(primaryTier, hybridTier = null) {
  const primary = byTier.get(primaryTier) ?? null;
  if (!primary || !hybridTier) return primary;
  return EVOLUTION_REGISTRY.hybrid(primaryTier, hybridTier) ?? primary;
}

export function evolutionFile(primaryTier, hybridTier = null) {
  return evolutionArt(primaryTier, hybridTier)?.file ?? null;
}

// Capstones never change the image: each lends the figure an effect drawn over and around whatever image it has.
// Effects compose in a fixed order (layer, then id), each on its own depth band, so two capstones on one creature
// look the same whatever order they were bought in. `motion` is what reduced motion stills.
export const CAPSTONE_EFFECTS = Object.freeze([
  Object.freeze({ id: 'capstone-titan', tier: 'tier:titan:v1', kind: 'glow', colour: 0xe8a76f, layer: 0, motion: 'pulse' }),
  Object.freeze({ id: 'capstone-apex', tier: 'tier:apex:v1', kind: 'motes', colour: 0xb6e38a, layer: 1, motion: 'drift' }),
  Object.freeze({ id: 'capstone-archmage', tier: 'tier:archmage:v1', kind: 'aura', colour: 0x9fb4ff, layer: 2, motion: 'spin' }),
]);
const effectsByTier = new Map(CAPSTONE_EFFECTS.map(one => [one.tier, one]));
const effectsById = new Map(CAPSTONE_EFFECTS.map(one => [one.id, one]));
export const capstoneEffect = id => effectsById.get(id) ?? null;
export const capstoneEffectOf = tierId => effectsByTier.get(tierId) ?? null;
export function composeCapstones(ids) {
  return [...new Set(ids)].map(capstoneEffect).filter(Boolean).sort((a, b) => a.layer - b.layer || a.id.localeCompare(b.id, 'en')).map(one => one.id);
}
