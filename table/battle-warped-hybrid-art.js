// Owner-supplied Warped hybrid portrait intake. These variants are staged only:
// visual influences and the mutation-based display rules have not been approved yet.
// The ordering is the order in which the twelve images were presented.
const variants = [
  // number, Stage 3, canvas [width, height]
  [1, 'revenant', [1536, 1024]],
  [2, 'revenant', [1536, 1024]],
  [3, 'revenant', [1536, 784]],
  [4, 'revenant', [1536, 1024]],
  [5, 'transcendent', [1536, 1024]],
  [6, 'transcendent', [1536, 1024]],
  [7, 'transcendent', [1536, 1024]],
  [8, 'transcendent', [1536, 1024]],
  [9, 'cataclysm', [1536, 1024]],
  [10, 'cataclysm', [1536, 1024]],
  [11, 'cataclysm', [1536, 1024]],
  [12, 'cataclysm', [1536, 1024]],
];

export const WARPED_HYBRID_ART = Object.freeze(variants.map(([number, form, size]) => {
  const variant = String(number).padStart(2, '0');
  return Object.freeze({
    id: `warped-${form}-${variant}`,
    family: 'warped',
    stage: 3,
    primaryArtwork: `warped-stage3-${form}`,
    variant,
    file: `evolutions/hybrids/warped-${form}-${variant}.webp`,
    width: size[0],
    height: size[1],
  });
}));
