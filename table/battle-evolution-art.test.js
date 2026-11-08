import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { BASE_EVOLUTION_ART, EVOLUTION_ART, evolutionArt, evolutionFile } from './battle-evolution-art.js';

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
