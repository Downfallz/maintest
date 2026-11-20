// Brute Stage 3 portraits, in intake order (docs/art/brute-hybrid-aaa-intake.md).
// Tyrant uses the catalogue artwork id brute-stage3-oppressor.
const frenzied = 'brute-stage2-frenzied', ironhide = 'brute-stage2-ironhide', oppressor = 'brute-stage2-oppressor';
const variants = [
  // Stage 3, variant, qualifying influence, canvas, measured alpha >= 128 bounds
  ['oppressor', 'warped', { family: 'warped' }, [1536, 1024], [14, 4, 1531, 1017]],
  ['oppressor', 'predator', { family: 'predator' }, [1536, 1024], [14, 5, 1523, 1015]],
  ['oppressor', 'sibling', { artworks: [frenzied, ironhide] }, [1536, 1024], [17, 4, 1522, 1016]],
  ['oppressor', 'ultimate', null, [1536, 1024], [11, 11, 1529, 1011]],
  ['colossus', 'warped', { family: 'warped' }, [1536, 1024], [39, 19, 1494, 1008]],
  ['colossus', 'predator', { family: 'predator' }, [1536, 1024], [19, 16, 1522, 1007]],
  ['colossus', 'sibling', { artworks: [oppressor, frenzied] }, [1536, 1024], [38, 19, 1495, 1010]],
  ['colossus', 'ultimate', null, [1536, 1024], [16, 14, 1526, 1015]],
  ['ravager', 'warped', { family: 'warped' }, [1536, 1024], [14, 15, 1529, 1021]],
  ['ravager', 'predator', { family: 'predator' }, [1774, 887], [10, 6, 1759, 887]],
  ['ravager', 'sibling', { artworks: [oppressor, ironhide] }, [1536, 1024], [8, 12, 1533, 1020]],
  ['ravager', 'ultimate', null, [1536, 1024], [26, 15, 1515, 984]],
];

export const BRUTE_HYBRID_ART = variants.map(([form, variant, influence, [width, height], bounds]) => {
  const id = `brute-${form}-${variant}`;
  return { id, primaryArtwork: `brute-stage3-${form}`, influence, ultimate: variant === 'ultimate',
    file: `evolutions/hybrids/${id}.webp`, width, height, bounds };
});
