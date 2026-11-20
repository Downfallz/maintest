// Brute Stage 3 portraits, in intake order (docs/art/brute-hybrid-aaa-intake.md).
// Tyrant uses the catalogue artwork id brute-stage3-oppressor.
const frenzied = 'brute-stage2-frenzied', ironhide = 'brute-stage2-ironhide', oppressor = 'brute-stage2-oppressor';
const variants = [
  // Stage 3, variant, qualifying influence, canvas, measured alpha >= 128 bounds
  ['oppressor', 'warped', { family: 'warped' }, [1536, 1024], [10, 2, 1534, 1017]],
  ['oppressor', 'predator', { family: 'predator' }, [1536, 1024], [12, 0, 1526, 1016]],
  ['oppressor', 'sibling', { artworks: [frenzied, ironhide] }, [1536, 1024], [16, 2, 1525, 1017]],
  ['oppressor', 'ultimate', null, [1536, 1024], [10, 9, 1531, 1013]],
  ['colossus', 'warped', { family: 'warped' }, [1536, 1024], [37, 17, 1495, 1009]],
  ['colossus', 'predator', { family: 'predator' }, [1536, 1024], [16, 15, 1524, 1008]],
  ['colossus', 'sibling', { artworks: [oppressor, frenzied] }, [1536, 1024], [37, 16, 1498, 1010]],
  ['colossus', 'ultimate', null, [1536, 1024], [16, 12, 1527, 1016]],
  ['ravager', 'warped', { family: 'warped' }, [1536, 1024], [13, 14, 1532, 1023]],
  ['ravager', 'predator', { family: 'predator' }, [1774, 887], [7, 5, 1762, 887]],
  ['ravager', 'sibling', { artworks: [oppressor, ironhide] }, [1536, 1024], [8, 10, 1536, 1023]],
  ['ravager', 'ultimate', null, [1536, 1024], [25, 13, 1519, 987]],
];

export const BRUTE_HYBRID_ART = variants.map(([form, variant, influence, [width, height], bounds]) => {
  const id = `brute-${form}-${variant}`;
  return { id, primaryArtwork: `brute-stage3-${form}`, influence, ultimate: variant === 'ultimate',
    file: `evolutions/hybrids/${id}.webp`, width, height, bounds };
});
