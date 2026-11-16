import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { PREDATOR_HYBRID_ART } from './battle-predator-hybrid-art.js';
import { EVOLUTION_ART } from './battle-evolution-art.js';

test('all twelve approved Predator hybrid portraits are valid WebP files', () => {
  assert.equal(PREDATOR_HYBRID_ART.length, 12);
  assert.equal(new Set(PREDATOR_HYBRID_ART.map(asset => asset.file)).size, 12);
  for (const asset of PREDATOR_HYBRID_ART) {
    assert.equal(asset.family, 'predator');
    assert.equal(asset.stage, 3);
    const bytes = readFileSync(new URL(`battle-art/${asset.file}`, import.meta.url));
    assert.equal(bytes.toString('ascii', 0, 4), 'RIFF');
    assert.equal(bytes.toString('ascii', 8, 12), 'WEBP');
  }
});

test('each Predator Stage 3 form has four separately staged portraits', () => {
  const counts = Map.groupBy(PREDATOR_HYBRID_ART, asset => asset.primaryArtwork);
  assert.deepEqual([...counts.keys()].sort(), [
    'predator-stage3-blightweaver', 'predator-stage3-deathstalker', 'predator-stage3-soulreaver',
  ]);
  assert.ok([...counts.values()].every(assets => assets.length === 4));
  // Each names a Stage 3 Predator illustration the page knows, by its page id, never by a content id.
  for (const id of counts.keys()) assert.equal(EVOLUTION_ART.find(art => art.id === id)?.stage, 3);
});
