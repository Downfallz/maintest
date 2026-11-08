import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { figureGeometry, CREATURE_ART } from './battle-art.js';
import { BASE_EVOLUTION_ART, EVOLUTION_ART, CAPSTONE_EFFECTS, capstoneEffect, composeCapstones, createEvolutionRegistry, EVOLUTION_REGISTRY } from './battle-evolution-art.js';

const tiersRoot = new URL('../data/Tiers/', import.meta.url);
const tiers = readdirSync(tiersRoot).filter(name => name.endsWith('.json')).map(name => JSON.parse(readFileSync(new URL(name, tiersRoot), 'utf8')));
const authored = JSON.parse(readFileSync(new URL('../data/presentation/mutation-art.json', import.meta.url), 'utf8'));

test('every stage one to three package names exactly one registered illustration, and every illustration is named', () => {
  const named = tiers.filter(tier => tier.level <= 3).map(tier => authored[tier.id]?.artwork);
  assert.ok(named.every(Boolean)); assert.equal(new Set(named).size, named.length);
  assert.deepEqual([...named].sort((a, b) => a.localeCompare(b)), EVOLUTION_ART.filter(asset => asset.stage > 0).map(asset => asset.id).sort((a, b) => a.localeCompare(b)));
  for (const id of named) assert.ok(EVOLUTION_REGISTRY.art(id), id);
  assert.equal(BASE_EVOLUTION_ART.file, 'evolutions/base-creature.webp'); assert.equal(BASE_EVOLUTION_ART.stage, 0);
});
test('every registered illustration is a WebP file of the folder', () => {
  const paths = EVOLUTION_ART.map(asset => asset.file);
  assert.equal(new Set(paths).size, 22);
  assert.deepEqual(readdirSync(new URL('battle-art/evolutions/', import.meta.url)).filter(name => name.endsWith('.webp')).sort((a, b) => a.localeCompare(b)),
    paths.map(path => path.replace('evolutions/', '')).sort((a, b) => a.localeCompare(b)));
  for (const path of paths) {
    const bytes = readFileSync(new URL(`battle-art/${path}`, import.meta.url));
    assert.equal(bytes.toString('ascii', 0, 4), 'RIFF'); assert.equal(bytes.toString('ascii', 8, 12), 'WEBP');
  }
});
test('every image carries a silhouette inside its canvas and stands at its bottom centre', () => {
  for (const asset of EVOLUTION_ART) {
    const [left, top, right, bottom] = asset.bounds;
    assert.ok(left >= 0 && top >= 0 && right <= asset.width && bottom <= asset.height && left < right && top < bottom, asset.id);
    assert.deepEqual([...asset.ground], [(left + right) / 2, bottom], asset.id);
  }
});
test('a hybrid is one registered entry, found by the two illustrations it combines and by its id', () => {
  const registry = createEvolutionRegistry([{ id: 'transcendent-necrotic', dominant: 'warped-stage3-transcendent', hybrid: 'warped-stage2-necrotic',
    file: 'evolutions/hybrids/transcendent-necrotic.webp', size: [1536, 1024], bounds: [1, 2, 3, 4] }]);
  assert.equal(registry.hybrid('warped-stage3-transcendent', 'warped-stage2-necrotic').file, 'evolutions/hybrids/transcendent-necrotic.webp');
  assert.equal(registry.art('transcendent-necrotic').hybrid, 'warped-stage2-necrotic');
  assert.equal(registry.hybrid('warped-stage2-necrotic', 'warped-stage3-transcendent'), null);
  assert.equal(registry.art('warped-stage3-transcendent').stage, 3);
  assert.equal(EVOLUTION_REGISTRY.hybrid('warped-stage3-transcendent', 'warped-stage2-necrotic'), null);
});
test('every capstone lends one registered effect, and effects compose in their fixed layer order', () => {
  const capstones = tiers.filter(tier => tier.level >= 4);
  assert.deepEqual(capstones.map(tier => authored[tier.id]?.effect).sort((a, b) => a.localeCompare(b)), CAPSTONE_EFFECTS.map(one => one.id).sort((a, b) => a.localeCompare(b)));
  for (const tier of capstones) { assert.ok(capstoneEffect(authored[tier.id].effect)); assert.equal(authored[tier.id].artwork, undefined); }
  assert.deepEqual(composeCapstones(['capstone-archmage', 'capstone-titan', 'capstone-titan', 'unknown']), ['capstone-titan', 'capstone-archmage']);
});
test('every illustration is drawn to the same box as the audited sprites, so no stage looks smaller than the one before', () => {
  const home = { x: 0, y: 0, size: 180, hitWidth: 200 };
  for (const asset of [...EVOLUTION_ART, ...CREATURE_ART]) {
    const { scale } = figureGeometry(asset, home, false);
    const [left, top, right, bottom] = asset.bounds;
    const width = (right - left) * scale, height = (bottom - top) * scale;
    // The silhouette fills the box on one side and never overflows the other.
    assert.ok(width <= home.hitWidth + 1e-9 && height <= home.size + 1e-9, asset.id ?? asset.form);
    assert.ok(Math.abs(width - home.hitWidth) < 1e-9 || Math.abs(height - home.size) < 1e-9, asset.id ?? asset.form);
  }
});
