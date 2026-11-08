// The evolution illustrations, separate from the four eagerly loaded combat sprites (battle-art.js), and the
// capstone effects, each under a stable id of its own. Which package draws which illustration, or lends which
// effect, is presentation authored beside the content (data/presentation/mutation-art.json, `artwork` and `effect`)
// and served with the catalogue's packages: the page names no package, and nothing is read off a filename.
// Hybrid images are registered one approved pair at a time, never as the power set of mutations.
const directory = 'evolutions/';

// Each image's silhouette: the box its opaque pixels (alpha >= 128) fill, measured from the file, and the point it
// stands on, the box's horizontal centre on its bottom edge, as the audited sprites use. Bounds, not canvases, set
// the drawn size, so a Stage 3 drawn on a busier canvas is never smaller than the Stage 2 before it.
const entries = [
  // id, family, stage, [width, height], [left, top, right, bottom]
  ['base-creature', 'base', 0, [1536, 1024], [139, 47, 1368, 999]],
  ['brute-base', 'brute', 1, [1536, 1024], [74, 10, 1457, 987]],
  ['predator-base', 'predator', 1, [1774, 887], [10, 9, 1767, 880]],
  ['warped-base', 'warped', 1, [1536, 1024], [179, 22, 1372, 1002]],
  ['brute-stage2-oppressor', 'brute', 2, [1536, 1024], [24, 7, 1515, 1010]],
  ['brute-stage2-frenzied', 'brute', 2, [1536, 1024], [13, 8, 1531, 1022]],
  ['brute-stage2-ironhide', 'brute', 2, [1536, 1024], [108, 89, 1450, 953]],
  ['predator-stage2-scythe', 'predator', 2, [1774, 887], [10, 3, 1764, 886]],
  ['predator-stage2-blighted', 'predator', 2, [1536, 1024], [8, 1, 1529, 1024]],
  ['predator-stage2-open-maw', 'predator', 2, [1671, 941], [5, 3, 1666, 938]],
  ['warped-stage2-stormborn', 'warped', 2, [1536, 1024], [13, 8, 1500, 1014]],
  ['warped-stage2-necrotic', 'warped', 2, [1536, 1024], [26, 3, 1440, 1021]],
  ['warped-stage2-ethereal', 'warped', 2, [1536, 1024], [22, 8, 1488, 1017]],
  ['brute-stage3-oppressor', 'brute', 3, [1536, 1024], [16, 5, 1525, 1017]],
  ['brute-stage3-colossus', 'brute', 3, [1536, 1024], [39, 19, 1495, 1009]],
  ['brute-stage3-ravager', 'brute', 3, [1536, 1024], [7, 4, 1536, 1024]],
  ['predator-stage3-soulreaver', 'predator', 3, [1536, 1024], [7, 2, 1531, 1019]],
  ['predator-stage3-blightweaver', 'predator', 3, [1536, 1024], [7, 4, 1532, 1024]],
  ['predator-stage3-deathstalker', 'predator', 3, [1536, 1024], [15, 8, 1525, 1022]],
  ['warped-stage3-cataclysm', 'warped', 3, [1536, 1024], [10, 6, 1529, 1018]],
  ['warped-stage3-revenant', 'warped', 3, [1536, 1024], [14, 8, 1523, 1021]],
  ['warped-stage3-transcendent', 'warped', 3, [1536, 1024], [18, 4, 1519, 1020]],
];

const asset = ({ id, family, stage, dominant = null, hybrid = null, size, bounds, file = `${directory}${id}.webp` }) => {
  const [left, , right, bottom] = bounds;
  return Object.freeze({ id, family, stage, dominant, hybrid, key: `evolution-${id}`, file,
    width: size[0], height: size[1], bounds: Object.freeze([...bounds]), ground: Object.freeze([(left + right) / 2, bottom]) });
};

export const EVOLUTION_ART = Object.freeze(entries.map(([id, family, stage, size, bounds]) => asset({ id, family, stage, size, bounds })));

export const BASE_EVOLUTION_ART = EVOLUTION_ART.find(one => one.stage === 0);

// Approved hybrid illustrations: a Stage 3's own image reworked with another mutation's characteristics, keyed by
// the two illustrations it combines. Adding one is one entry, its identity, file and silhouette; the resolver
// (battle-evolution.js) is never edited for it. Once its art is approved and committed, an entry reads:
//   { id: 'transcendent-necrotic', dominant: 'warped-stage3-transcendent', hybrid: 'warped-stage2-necrotic',
//     file: 'evolutions/hybrids/transcendent-necrotic.webp', size: [1536, 1024], bounds: [...] }
const hybrids = [];

export const HYBRID_ART = Object.freeze(hybrids.map(one => asset({ ...one, family: 'hybrid', stage: 3 })));

const byId = new Map(EVOLUTION_ART.map(one => [one.id, one]));
const pairKey = (dominant, hybrid) => `${dominant}+${hybrid}`;

// A registry: an image by its stable id, the approved hybrid of two illustrations, the ancestral image. A pair
// nobody drew is null; only the resolver decides what shows in its place.
export function createEvolutionRegistry(hybridArt = HYBRID_ART) {
  const pairs = new Map(hybridArt.map(one => [pairKey(one.dominant, one.hybrid), one]));
  const ids = new Map([...byId, ...hybridArt.map(one => [one.id, one])]);
  return {
    art: id => ids.get(id) ?? null,
    hybrid: (dominant, hybrid) => pairs.get(pairKey(dominant, hybrid)) ?? null,
    base: () => BASE_EVOLUTION_ART,
  };
}

export const EVOLUTION_REGISTRY = createEvolutionRegistry();

// Capstones never change the image: each lends the figure an effect drawn over and around whatever image it has.
// Effects compose in a fixed order (layer, then id), each on its own depth band, so two capstones on one creature
// look the same whatever order they were bought in. `motion` is what reduced motion stills.
export const CAPSTONE_EFFECTS = Object.freeze([
  Object.freeze({ id: 'capstone-titan', kind: 'glow', colour: 0xe8a76f, layer: 0, motion: 'pulse' }),
  Object.freeze({ id: 'capstone-apex', kind: 'motes', colour: 0xb6e38a, layer: 1, motion: 'drift' }),
  Object.freeze({ id: 'capstone-archmage', kind: 'aura', colour: 0x9fb4ff, layer: 2, motion: 'spin' }),
]);
const effectsById = new Map(CAPSTONE_EFFECTS.map(one => [one.id, one]));
export const capstoneEffect = id => effectsById.get(id) ?? null;
export function composeCapstones(ids) {
  return [...new Set(ids)].map(capstoneEffect).filter(Boolean).sort((a, b) => a.layer - b.layer || a.id.localeCompare(b.id, 'en')).map(one => one.id);
}
