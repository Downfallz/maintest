import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { PAGE_ART } from './page-art.js';

// A spell's portrait is named by its visual (data/presentation/spell-art.json), a capstone's by its own
// (data/presentation/mutation-art.json), and both are drawn from the picture kit as `spell-<name>` (ADR 0198). A
// name the kit does not hold draws nothing, silently, so the names and the kit's declarations are held together
// here, both ways.
const read = path => JSON.parse(readFileSync(new URL(`../data/presentation/${path}`, import.meta.url), 'utf8'));
const named = [read('spell-art.json'), read('mutation-art.json')].flatMap(art => Object.values(art))
  .map(visual => visual.portrait).filter(Boolean).map(name => `spell-${name}`);
const declared = Object.keys(PAGE_ART).filter(name => name.startsWith('spell-'));

test('every portrait a spell or a capstone names is a picture of the kit, and every spell picture of the kit is named', () => {
  assert.ok(named.length > 0);
  assert.equal(new Set(named).size, named.length);
  assert.deepEqual([...named].sort((a, b) => a.localeCompare(b)), [...declared].sort((a, b) => a.localeCompare(b)));
});

test('every spell portrait is declared for the battle page at the 40 px of a card and the 24 px of a row, and elsewhere at 40', () => {
  // The battle draws a portrait at 24 px on a short screen and in the journal (docs/art/journeys/windows.md), so
  // its budget counts both; the field guide and the statistics draw it at 40 only.
  for (const name of declared) {
    assert.deepEqual(PAGE_ART[name].drawn, [40, 24], name);
    assert.deepEqual(PAGE_ART[name].pages, { 'table/battle': 'lazy', 'showcase/mutations': 'lazy', 'table/stats': 'lazy' }, name);
    assert.deepEqual(PAGE_ART[name].phone, { 'table/battle': [40, 24] }, name);
    assert.deepEqual(PAGE_ART[name].sizes.map(size => size.width), [24, 40, 48, 80], name);
  }
});

test('every form a mutation leads to is declared for the battle page at 64 px, lazy, and for the statistics at 144', () => {
  // The pick and the Mutation Tree draw the form a mutation's `artwork` names (data/presentation/mutation-art.json),
  // on each mutation and in the detail; a capstone names a portrait and no artwork, and keeps its portrait.
  const artworks = [...new Set(Object.values(read('mutation-art.json')).map(visual => visual.artwork).filter(Boolean))];
  assert.equal(artworks.length, 21);
  for (const artwork of artworks) {
    const picture = PAGE_ART[`mutation-${artwork}`];
    assert.ok(picture, `mutation-${artwork} is declared and derived`);
    assert.deepEqual(picture.drawn, [144, 64], artwork);
    assert.deepEqual(picture.pages, { 'table/stats': 'lazy', 'table/battle': 'lazy' }, artwork);
    assert.deepEqual(picture.phone, { 'table/battle': [64] }, artwork);
    assert.ok([64, 128].every(width => picture.sizes.some(size => size.width === width)), `${artwork} is sent at 64 and 128 px`);
  }
});

test('the spellbook draws a portrait only at widths the battle page declares, the 24 px under 660 px tall', async () => {
  // A size the declaration does not hold would be scaled down from a larger file, and counted by no budget.
  const { SPELLBOOK_PORTRAIT_SIZES } = await import('./battle-spellbook.js');
  const widths = [...SPELLBOOK_PORTRAIT_SIZES.matchAll(/(\d+)px(?:,|$)/g)].map(match => Number(match[1]));
  assert.deepEqual(widths, [24, 40]);
  assert.match(SPELLBOOK_PORTRAIT_SIZES, /^\(orientation: portrait\) and \(max-height: 660px\) 24px, 40px$/);
  for (const name of declared) assert.ok(widths.every(width => PAGE_ART[name].phone['table/battle'].includes(width)), name);
});

test('the journal draws a portrait only at 24 px, a width the battle page declares', () => {
  // The journal once drew the portrait at 22 px, scaled from a file of another size (UX12 P4). Every call names its
  // width: one without would take the card's 40 px.
  const source = readFileSync(new URL('../battle/src/history.ts', import.meta.url), 'utf8');
  const widths = [...source.matchAll(/spellPortrait\([^,]+,\s*(\d+)\)/g)].map(match => Number(match[1]));
  assert.equal([...source.matchAll(/spellPortrait\(/g)].length, widths.length);
  assert.deepEqual(widths, [24, 24]);
  for (const name of declared) assert.ok(PAGE_ART[name].phone['table/battle'].includes(24), name);
  const css = readFileSync(new URL('./battle.css', import.meta.url), 'utf8');
  assert.match(css, /#history-dialog \.spell-portrait \{ width: 24px; height: 24px;/);
});

test('the pick and the Mutation Tree draw a form only at 64 px, from a mutation the catalogue names, declared for the battle page', () => {
  // A mutation's form is its `artwork` (data/presentation/mutation-art.json), drawn as `mutation-<artwork>` (UX12 P5).
  // A name the kit does not hold draws nothing, silently: every artwork is held, for the battle page, at 64 px.
  const artworks = Object.values(read('mutation-art.json')).map(visual => visual.artwork).filter(Boolean);
  assert.ok(artworks.length > 0);
  for (const artwork of artworks) assert.ok(PAGE_ART[`mutation-${artwork}`]?.phone['table/battle'].includes(64), artwork);
  const portrait = readFileSync(new URL('../battle/src/spell-portrait.ts', import.meta.url), 'utf8');
  assert.match(portrait, /export function mutationForm\(artwork\?: string \| null, drawn = 64\)/);
  for (const file of ['mutation-tree.ts', 'mutation-atlas.ts']) {
    const source = readFileSync(new URL(`../battle/src/${file}`, import.meta.url), 'utf8');
    // Every call takes the default width: one that names another would scale a 64 px file.
    const calls = [...source.matchAll(/mutationForm\(([^()]*(?:\([^()]*\))?[^()]*)\)/g)];
    assert.ok(calls.length > 0, file);
    assert.equal([...source.matchAll(/mutationForm\(/g)].length, calls.length, file);
    for (const call of calls) assert.doesNotMatch(call[1], /,/, file);
  }
  const css = readFileSync(new URL('./battle.css', import.meta.url), 'utf8');
  assert.match(css, /\.mutation-form \{ width: 64px; height: auto; aspect-ratio: 3 \/ 2;/);
});

test('the Tempo sheet and the order of play draw a portrait only at 24 px, a width the battle page declares', () => {
  // UX12 P7: the files are P2's 24 and 48 px, so the page's total does not move. A call without a width would take the
  // card's 40 px.
  const order = readFileSync(new URL('../battle/src/order.ts', import.meta.url), 'utf8');
  assert.deepEqual([...order.matchAll(/spellPortrait\([^,]+,\s*(\d+)\)/g)].map(match => Number(match[1])), [24]);
  assert.equal([...order.matchAll(/spellPortrait\(/g)].length, 1);
  const tempo = readFileSync(new URL('../battle/src/tempo.ts', import.meta.url), 'utf8');
  assert.deepEqual([...tempo.matchAll(/portraitSizes: '(\d+)px'/g)].map(match => Number(match[1])), [24]);
  for (const name of declared) assert.ok(PAGE_ART[name].phone['table/battle'].includes(24), name);
  const css = readFileSync(new URL('./battle.css', import.meta.url), 'utf8');
  assert.match(css, /\.tempo-spells \.spell-card > \.spell-portrait \{ width: 24px; height: 24px;/);
  assert.match(css, /#timeline li > button > \.spell-portrait \{ flex: none; width: 24px; height: 24px; \}/);
});
