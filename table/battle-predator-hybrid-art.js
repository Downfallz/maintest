// Owner-supplied Predator hybrid portraits, staged separately from combat sprites.
// Variants are numbered by visual intake order until a tier-pair mapping is approved. Each names the Stage 3
// illustration it varies by its page id (battle-evolution-art.js): which package draws that illustration is
// presentation data served with the catalogue (data/presentation/mutation-art.json), never a content id here.
const variants = [
  ['soulreaver', 'predator-stage3-soulreaver', [1, 2, 3, 4]],
  ['deathstalker', 'predator-stage3-deathstalker', [5, 6, 7, 8]],
  ['blightweaver', 'predator-stage3-blightweaver', [9, 10, 11, 12]],
];

export const PREDATOR_HYBRID_ART = Object.freeze(variants.flatMap(([form, primaryArtwork, numbers]) =>
  numbers.map(number => {
    const variant = String(number).padStart(2, '0');
    return Object.freeze({
      id: `predator-${form}-${variant}`,
      family: 'predator',
      stage: 3,
      primaryArtwork,
      variant,
      file: `evolutions/hybrids/predator-${form}-${variant}.webp`,
      width: number === 9 ? 1754 : 1536,
      height: number === 9 ? 896 : 1024,
    });
  })));
