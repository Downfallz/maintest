// The owner-approved Warped hybrid portraits (docs/art/warped-hybrid-aaa-intake.md, ADR 0152): each is a Stage 3
// Warped reworked with the traits of the creature's other mutations. Every entry names the page's own illustration
// ids, never a content id: which package draws which illustration is presentation data served with the catalogue
// (data/presentation/mutation-art.json).
//
// An entry varies one Stage 3 (`primaryArtwork`) and shows for one influence of the creature's other mutations, as the
// Predator portraits do (battle-predator-hybrid-art.js):
//   { artworks: [...] }  one of these illustrations is held: another Warped line's Stage 2
//   { family: '...' }    a mutation of that family is held
//   'ultimate'           every other influence drawn for that Stage 3 is held at Stage 2 (70% / 30% of them all)
const stormborn = 'warped-stage2-stormborn', necrotic = 'warped-stage2-necrotic', ethereal = 'warped-stage2-ethereal';
const variants = [
  // Stage 3, influence name, influence, [width, height], silhouette [left, top, right, bottom] (alpha >= 128, as measured)
  ['cataclysm', 'sibling', { artworks: [necrotic, ethereal] }, [1536, 1024], [16, 8, 1512, 1014]],
  ['cataclysm', 'predator', { family: 'predator' }, [1536, 1024], [12, 9, 1517, 1016]],
  ['cataclysm', 'brute', { family: 'brute' }, [1536, 1024], [25, 9, 1513, 1015]],
  ['cataclysm', 'ultimate', 'ultimate', [1536, 1024], [22, 17, 1516, 1009]],
  ['revenant', 'sibling', { artworks: [stormborn, ethereal] }, [1536, 1024], [6, 8, 1525, 1019]],
  ['revenant', 'predator', { family: 'predator' }, [1536, 1024], [3, 9, 1529, 1017]],
  ['revenant', 'brute', { family: 'brute' }, [1536, 1024], [6, 6, 1527, 1019]],
  ['revenant', 'ultimate', 'ultimate', [1536, 1024], [7, 5, 1531, 1020]],
  ['transcendent', 'sibling', { artworks: [stormborn, necrotic] }, [1536, 1024], [7, 10, 1526, 1012]],
  ['transcendent', 'predator', { family: 'predator' }, [1536, 1024], [0, 1, 1536, 1022]],
  ['transcendent', 'brute', { family: 'brute' }, [1536, 1024], [5, 5, 1510, 1014]],
  ['transcendent', 'ultimate', 'ultimate', [1536, 784], [7, 0, 1518, 784]],
];

export const WARPED_HYBRID_ART = Object.freeze(variants.map(([form, name, influence, size, bounds]) => Object.freeze({
  id: `warped-${form}-${name}`,
  family: 'warped',
  stage: 3,
  primaryArtwork: `warped-stage3-${form}`,
  influence: influence === 'ultimate' ? null
    : Object.freeze(influence.artworks ? { artworks: Object.freeze([...influence.artworks]) } : { family: influence.family }),
  ultimate: influence === 'ultimate',
  file: `evolutions/hybrids/warped-${form}-${name}.webp`,
  width: size[0],
  height: size[1],
  bounds: Object.freeze([...bounds]),
})));
