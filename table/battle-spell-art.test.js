import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { PAGE_ART } from './page-art.js';

// A spell's portrait is named by its visual (data/presentation/spell-art.json) and drawn from the picture kit as
// `spell-<name>` (ADR 0198). A name the kit does not hold draws nothing, silently, so the names and the kit's
// declarations are held together here, both ways.
const authored = JSON.parse(readFileSync(new URL('../data/presentation/spell-art.json', import.meta.url), 'utf8'));
const named = Object.values(authored).map(visual => visual.portrait).filter(Boolean).map(name => `spell-${name}`);
const declared = Object.keys(PAGE_ART).filter(name => name.startsWith('spell-'));

test('every portrait a spell names is a picture of the kit, and every spell picture of the kit is named', () => {
  assert.ok(named.length > 0);
  assert.equal(new Set(named).size, named.length);
  assert.deepEqual([...named].sort((a, b) => a.localeCompare(b)), [...declared].sort((a, b) => a.localeCompare(b)));
});

test('every spell portrait is declared for the battle page at the 40 px the spell card draws it', () => {
  for (const name of declared) {
    assert.deepEqual(PAGE_ART[name].drawn, [40], name);
    assert.deepEqual(PAGE_ART[name].pages, { 'table/battle': 'lazy' }, name);
  }
});
