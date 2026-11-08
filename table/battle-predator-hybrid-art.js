// Owner-supplied Predator hybrid portraits, staged separately from combat sprites.
// Variants are numbered by visual intake order until a tier-pair mapping is approved.
const variants = [
  ['soulreaver', 'tier:soulreaver:v1', [1, 2, 3, 4]],
  ['deathstalker', 'tier:deathstalker:v1', [5, 6, 7, 8]],
  ['blightweaver', 'tier:blightweaver:v1', [9, 10, 11, 12]],
];

export const PREDATOR_HYBRID_ART = Object.freeze(variants.flatMap(([form, primaryTier, numbers]) =>
  numbers.map(number => {
    const variant = String(number).padStart(2, '0');
    return Object.freeze({
      id: `predator-${form}-${variant}`,
      family: 'predator',
      stage: 3,
      primaryTier,
      variant,
      file: `evolutions/hybrids/predator-${form}-${variant}.webp`,
      width: number === 9 ? 1754 : 1536,
      height: number === 9 ? 896 : 1024,
    });
  })));
