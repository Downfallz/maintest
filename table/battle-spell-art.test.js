import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';

// A spell's portrait is named by its visual (data/presentation/spell-art.json) and served from one folder, listed
// when the host starts. A name typed wrong only shows as a missing picture, so the names and the files are held
// together here, both ways.
const authored = JSON.parse(readFileSync(new URL('../data/presentation/spell-art.json', import.meta.url), 'utf8'));
const named = Object.values(authored).map(visual => visual.portrait).filter(Boolean);
const folder = new URL('battle-art/spells/', import.meta.url);
const files = readdirSync(folder).filter(name => name.endsWith('.webp')).map(name => name.slice(0, -'.webp'.length));

// The canvas size a WebP declares, whichever of its three forms it is in.
function size(bytes) {
  const form = bytes.toString('ascii', 12, 16);
  if (form === 'VP8X') return [1 + bytes.readUIntLE(24, 3), 1 + bytes.readUIntLE(27, 3)];
  if (form === 'VP8L') { const bits = bytes.readUInt32LE(21); return [1 + (bits & 0x3fff), 1 + ((bits >> 14) & 0x3fff)]; }
  return [bytes.readUInt16LE(26) & 0x3fff, bytes.readUInt16LE(28) & 0x3fff];
}

test('every portrait a spell names is a file of the folder, and every file there is named', () => {
  assert.ok(named.length > 0);
  assert.equal(new Set(named).size, named.length);
  assert.deepEqual([...named].sort((a, b) => a.localeCompare(b)), [...files].sort((a, b) => a.localeCompare(b)));
});

test('every portrait is a square WebP sent at twice the 40 px the spell card draws it', () => {
  for (const name of files) {
    const bytes = readFileSync(new URL(`${name}.webp`, folder));
    assert.equal(bytes.toString('ascii', 0, 4), 'RIFF', name); assert.equal(bytes.toString('ascii', 8, 12), 'WEBP', name);
    assert.deepEqual(size(bytes), [80, 80], name);
  }
});
