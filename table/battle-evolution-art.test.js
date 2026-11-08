import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { BASE_EVOLUTION_ART, EVOLUTION_ART, CAPSTONE_EFFECTS, capstoneEffectOf, composeCapstones, createEvolutionRegistry, evolutionArt, evolutionFile } from './battle-evolution-art.js';

test('every stage one to three package has exactly one registered evolution image', () => {
  const directory = new URL('../data/Tiers/', import.meta.url);
  const tiers = readdirSync(directory).filter(name => name.endsWith('.json'))
    .map(name => JSON.parse(readFileSync(new URL(name, directory), 'utf8')))
    .filter(tier => tier.level <= 3);
  assert.deepEqual(EVOLUTION_ART.filter(asset => asset.tier).map(asset => asset.tier).sort(), tiers.map(tier => tier.id).sort());
  const paths = EVOLUTION_ART.map(asset => asset.file);
  assert.equal(new Set(paths).size, 22);
  assert.equal(BASE_EVOLUTION_ART.file, 'evolutions/base-creature.webp');
  assert.equal(BASE_EVOLUTION_ART.stage, 0);
  for (const path of paths) {
    const bytes = readFileSync(new URL(`battle-art/${path}`, import.meta.url));
    assert.equal(bytes.toString('ascii', 0, 4), 'RIFF');
    assert.equal(bytes.toString('ascii', 8, 12), 'WEBP');
  }
  assert.equal(new Set(EVOLUTION_ART.filter(asset => asset.tier).map(asset => asset.tier)).size, 21);
});

test('unknown or not-yet-authored hybrid assets fall back to the primary transformation', () => {
  const primary = 'tier:spiritcaller:v1';
  assert.equal(evolutionArt(primary)?.id, 'warped-stage3-transcendent');
  assert.equal(evolutionArt(primary, 'tier:necromancer:v1'), evolutionArt(primary));
  assert.equal(evolutionFile(primary), 'evolutions/warped-stage3-transcendent.webp');
  assert.equal(evolutionArt('tier:unknown:v1'), null);
});

test('every image carries a silhouette inside its canvas and stands at its bottom centre', () => {
  for (const asset of EVOLUTION_ART) {
    const [left, top, right, bottom] = asset.bounds;
    assert.ok(left >= 0 && top >= 0 && right <= asset.width && bottom <= asset.height && left < right && top < bottom, asset.id);
    assert.deepEqual([...asset.ground], [(left + right) / 2, bottom], asset.id);
  }
});
test('a hybrid is one registered entry, found by its pair and by its id, and nothing else changes', () => {
  const registry = createEvolutionRegistry([{ id: 'transcendent-necrotic', dominantTierId: 'tier:spiritcaller:v1', hybridTierId: 'tier:necromancer:v1',
    family: 'hybrid', stage: 3, file: 'evolutions/hybrids/transcendent-necrotic.webp', bounds: [1, 2, 3, 4], ground: [2, 4] }]);
  assert.equal(registry.hybrid('tier:spiritcaller:v1', 'tier:necromancer:v1').file, 'evolutions/hybrids/transcendent-necrotic.webp');
  assert.equal(registry.art('transcendent-necrotic').hybridTierId, 'tier:necromancer:v1');
  assert.equal(registry.hybrid('tier:necromancer:v1', 'tier:spiritcaller:v1'), null);
  assert.equal(registry.tier('tier:spiritcaller:v1').id, 'warped-stage3-transcendent');
});
test('every capstone has one effect, and effects compose in their fixed layer order', () => {
  const directory = new URL('../data/Tiers/', import.meta.url);
  const capstones = readdirSync(directory).filter(name => name.endsWith('.json')).map(name => JSON.parse(readFileSync(new URL(name, directory), 'utf8')))
    .filter(tier => tier.level >= 4).map(tier => tier.id).sort();
  assert.deepEqual(CAPSTONE_EFFECTS.map(one => one.tier).sort(), capstones);
  for (const id of capstones) assert.ok(capstoneEffectOf(id));
  assert.deepEqual(composeCapstones(['capstone-archmage', 'capstone-titan', 'capstone-titan', 'unknown']), ['capstone-titan', 'capstone-archmage']);
});
