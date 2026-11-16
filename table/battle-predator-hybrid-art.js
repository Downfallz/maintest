// The owner-approved Predator hybrid portraits (docs/art/predator-hybrid-aaa-intake.md, ADR 0152): each is a Stage 3
// Predator reworked with the traits of the creature's other mutations. Files are numbered in the order of the
// approved batch. Every entry names the page's own illustration ids, never a content id: which package draws which
// illustration is presentation data served with the catalogue (data/presentation/mutation-art.json).
//
// An entry varies one Stage 3 (`primaryArtwork`) and shows for one influence of the creature's other mutations:
//   { artworks: [...] }  one of these illustrations is held: another Predator line's Stage 2
//   { family: '...' }    a mutation of that family is held
//   'ultimate'           every other influence drawn for that Stage 3 is held at Stage 2 (70% / 30% of them all)
const blightedLine = 'predator-stage2-blighted', parasiteLine = 'predator-stage2-open-maw';
const variants = [
  // number, Stage 3, influence, [width, height], silhouette [left, top, right, bottom] (alpha >= 128, as measured)
  [1, 'soulreaver', { artworks: [blightedLine] }, [1536, 1024], [4, 4, 1533, 1023]],
  [2, 'soulreaver', { family: 'warped' }, [1536, 1024], [9, 2, 1528, 1023]],
  [3, 'soulreaver', { family: 'brute' }, [1536, 1024], [4, 3, 1535, 1022]],
  [4, 'soulreaver', 'ultimate', [1536, 1024], [16, 6, 1521, 1017]],
  [5, 'deathstalker', { artworks: [blightedLine, parasiteLine] }, [1536, 1024], [8, 8, 1528, 1020]],
  [6, 'deathstalker', { family: 'warped' }, [1536, 1024], [11, 6, 1527, 1021]],
  [7, 'deathstalker', { family: 'brute' }, [1536, 1024], [9, 6, 1529, 1021]],
  [8, 'deathstalker', 'ultimate', [1536, 1024], [6, 6, 1533, 1023]],
  [9, 'blightweaver', { artworks: [parasiteLine] }, [1754, 896], [6, 3, 1747, 891]],
  [10, 'blightweaver', { family: 'brute' }, [1536, 1024], [15, 3, 1528, 1022]],
  [11, 'blightweaver', { family: 'warped' }, [1536, 1024], [10, 4, 1526, 1024]],
  [12, 'blightweaver', 'ultimate', [1536, 1024], [4, 5, 1534, 1021]],
];

export const PREDATOR_HYBRID_ART = Object.freeze(variants.map(([number, form, influence, size, bounds]) => {
  const variant = String(number).padStart(2, '0');
  return Object.freeze({
    id: `predator-${form}-${variant}`,
    family: 'predator',
    stage: 3,
    primaryArtwork: `predator-stage3-${form}`,
    variant,
    influence: influence === 'ultimate' ? null
      : Object.freeze(influence.artworks ? { artworks: Object.freeze([...influence.artworks]) } : { family: influence.family }),
    ultimate: influence === 'ultimate',
    file: `evolutions/hybrids/predator-${form}-${variant}.webp`,
    width: size[0],
    height: size[1],
    bounds: Object.freeze([...bounds]),
  });
}));
