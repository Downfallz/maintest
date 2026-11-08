import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { WARPED_HYBRID_ART } from './battle-warped-hybrid-art.js';
import { EVOLUTION_ART } from './battle-evolution-art.js';

test('all twelve Warped hybrid portraits are distinct valid WebP assets', () => {
  assert.equal(WARPED_HYBRID_ART.length, 12);
  assert.equal(new Set(WARPED_HYBRID_ART.map(asset => asset.file)).size, 12);
  for (const asset of WARPED_HYBRID_ART) {
    assert.equal(asset.family, 'warped');
    assert.equal(asset.stage, 3);
    const bytes = readFileSync(new URL(`battle-art/${asset.file}`, import.meta.url));
    assert.equal(bytes.toString('ascii', 0, 4), 'RIFF');
    assert.equal(bytes.toString('ascii', 8, 12), 'WEBP');
  }
});

test('each existing Warped Stage 3 illustration has four staged portraits', () => {
  const groups = Map.groupBy(WARPED_HYBRID_ART, art => art.primaryArtwork);
  assert.deepEqual([...groups.keys()].sort(), [
    'warped-stage3-cataclysm', 'warped-stage3-revenant', 'warped-stage3-transcendent',
  ]);
  for (const [id, assets] of groups) {
    assert.equal(assets.length, 4);
    assert.equal(EVOLUTION_ART.find(art => art.id === id)?.stage, 3);
  }
});
