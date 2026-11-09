import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { WARPED_HYBRID_ART } from './battle-warped-hybrid-art.js';
import { EVOLUTION_ART } from './battle-evolution-art.js';

const bytesOf = asset => readFileSync(new URL(`battle-art/${asset.file}`, import.meta.url));

// The canvas a WebP declares, and whether it carries alpha: lossy (VP8), lossless (VP8L) or extended (VP8X).
function webp(bytes) {
  const chunk = bytes.toString('ascii', 12, 16);
  if (chunk === 'VP8X') return { size: [1 + bytes.readUIntLE(24, 3), 1 + bytes.readUIntLE(27, 3)], alpha: (bytes[20] & 0x10) !== 0 };
  if (chunk === 'VP8L') { const bits = bytes.readUInt32LE(21); return { size: [(bits & 0x3fff) + 1, ((bits >>> 14) & 0x3fff) + 1], alpha: ((bits >>> 28) & 1) === 1 }; }
  if (chunk === 'VP8 ') return { size: [bytes.readUInt16LE(26) & 0x3fff, bytes.readUInt16LE(28) & 0x3fff], alpha: false };
  throw new Error(`Unknown WebP chunk ${chunk}`);
}

test('all twelve approved Warped hybrid portraits are distinct valid WebP files', () => {
  assert.equal(WARPED_HYBRID_ART.length, 12);
  assert.equal(new Set(WARPED_HYBRID_ART.map(asset => asset.file)).size, 12);
  assert.equal(new Set(WARPED_HYBRID_ART.map(asset => asset.id)).size, 12);
  for (const asset of WARPED_HYBRID_ART) {
    assert.equal(asset.family, 'warped');
    assert.equal(asset.stage, 3);
    const bytes = bytesOf(asset);
    assert.equal(bytes.toString('ascii', 0, 4), 'RIFF');
    assert.equal(bytes.toString('ascii', 8, 12), 'WEBP');
  }
});

test('each portrait keeps the canvas its entry declares, and carries the alpha its silhouette was measured from', () => {
  // The sources came on opaque black; the committed files add alpha (scripts/matte-opaque-art.py), never a new canvas.
  for (const asset of WARPED_HYBRID_ART) {
    const { size, alpha } = webp(bytesOf(asset));
    assert.deepEqual(size, [asset.width, asset.height], asset.file);
    assert.equal(alpha, true, asset.file);
  }
});

test('each Warped Stage 3 has a sibling-line, a Predator, a Brute and an Ultimate portrait', () => {
  const groups = Map.groupBy(WARPED_HYBRID_ART, asset => asset.primaryArtwork);
  assert.deepEqual([...groups.keys()].sort(), ['warped-stage3-cataclysm', 'warped-stage3-revenant', 'warped-stage3-transcendent']);
  for (const [id, assets] of groups) {
    // Each names a Stage 3 Warped illustration the page knows, by its page id, never by a content id.
    assert.equal(EVOLUTION_ART.find(art => art.id === id)?.stage, 3);
    assert.equal(assets.filter(asset => asset.ultimate).length, 1, id);
    assert.deepEqual(assets.filter(asset => asset.influence?.family).map(asset => asset.influence.family).sort(), ['brute', 'predator'], id);
    assert.equal(assets.filter(asset => asset.influence?.artworks).length, 1, id);
  }
});

test('a sibling-line portrait lends the two other Warped lines, never the one that leads to its own Stage 3', () => {
  // The Stage 2 each Warped Stage 3 descends from (data/Tiers: elementalist, necromancer, shaman).
  const leading = { 'warped-stage3-cataclysm': 'warped-stage2-stormborn', 'warped-stage3-revenant': 'warped-stage2-necrotic',
    'warped-stage3-transcendent': 'warped-stage2-ethereal' };
  const lines = ['warped-stage2-stormborn', 'warped-stage2-necrotic', 'warped-stage2-ethereal'];
  for (const asset of WARPED_HYBRID_ART.filter(one => one.influence?.artworks)) {
    assert.deepEqual([...asset.influence.artworks].sort(), lines.filter(line => line !== leading[asset.primaryArtwork]).sort(), asset.id);
    for (const id of asset.influence.artworks) assert.equal(EVOLUTION_ART.find(art => art.id === id)?.stage, 2, `${asset.id} ${id}`);
  }
});
