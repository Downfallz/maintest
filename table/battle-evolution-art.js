// Optional creature portraits, separate from the four eagerly loaded combat sprites.
// Keys use content IDs rather than display labels, so renaming a package never changes an asset path.
// Hybrid images can be registered later without enumerating the mutation power set.
const directory = 'evolutions/';
const sourceDimensions = Object.freeze({
  'predator-base': [1774, 887],
  'predator-stage2-scythe': [1774, 887],
  'predator-stage2-open-maw': [1671, 941],
});

const entries = [
  ['brute-base', 'brute', 1, 'tier:brute:v1'],
  ['predator-base', 'predator', 1, 'tier:prowler:v1'],
  ['warped-base', 'warped', 1, 'tier:occultist:v1'],
  ['brute-stage2-oppressor', 'brute', 2, 'tier:marauder:v1'],
  ['brute-stage2-frenzied', 'brute', 2, 'tier:berserker:v1'],
  ['brute-stage2-ironhide', 'brute', 2, 'tier:ironbound:v1'],
  ['predator-stage2-scythe', 'predator', 2, 'tier:assassin:v1'],
  ['predator-stage2-blighted', 'predator', 2, 'tier:plague_doctor:v1'],
  ['predator-stage2-open-maw', 'predator', 2, 'tier:parasite:v1'],
  ['warped-stage2-stormborn', 'warped', 2, 'tier:elementalist:v1'],
  ['warped-stage2-necrotic', 'warped', 2, 'tier:necromancer:v1'],
  ['warped-stage2-ethereal', 'warped', 2, 'tier:shaman:v1'],
  ['brute-stage3-oppressor', 'brute', 3, 'tier:warmonger:v1'],
  ['brute-stage3-colossus', 'brute', 3, 'tier:dreadnought:v1'],
  ['brute-stage3-ravager', 'brute', 3, 'tier:ravager:v1'],
  ['predator-stage3-soulreaver', 'predator', 3, 'tier:soulreaver:v1'],
  ['predator-stage3-blightweaver', 'predator', 3, 'tier:blightweaver:v1'],
  ['predator-stage3-deathstalker', 'predator', 3, 'tier:deathstalker:v1'],
  ['warped-stage3-cataclysm', 'warped', 3, 'tier:harbinger:v1'],
  ['warped-stage3-revenant', 'warped', 3, 'tier:lich:v1'],
  ['warped-stage3-transcendent', 'warped', 3, 'tier:spiritcaller:v1'],
];

export const EVOLUTION_ART = Object.freeze(entries.map(([id, family, stage, tier]) => Object.freeze({
  id, family, stage, tier, key: `evolution-${id}`, file: `${directory}${id}.webp`,
  width: (sourceDimensions[id] ?? [1536, 1024])[0],
  height: (sourceDimensions[id] ?? [1536, 1024])[1],
})));

// This polished reference is retained for visual review, but it has no audited package mapping.
export const EVOLUTION_UNASSIGNED = Object.freeze(['evolutions/predator-stage2-lean.webp']);

const byTier = new Map(EVOLUTION_ART.map(asset => [asset.tier, asset]));
const byHybrid = new Map();

// Called with explicit art registrations. The base Stage 3 remains the dominant image;
// only a known hybrid ever overrides it. Unknown extra purchases must not change the sprite.
export function evolutionArt(primaryTier, hybridTier = null) {
  const primary = byTier.get(primaryTier) ?? null;
  if (!primary || !hybridTier) return primary;
  return byHybrid.get(`${primaryTier}+${hybridTier}`) ?? primary;
}

export function evolutionFile(primaryTier, hybridTier = null) {
  return evolutionArt(primaryTier, hybridTier)?.file ?? null;
}
