import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { PAGE_ART } from './page-art.js';
import { BATTLE_PAGE, FAMILY_HEADS, FAMILY_HEAD_SIZES, RESULT_HEAD, TREE_HEAD } from './battle-scenes.js';

// The match's windows wear four scenes (docs/art/scene-paintings-plan.md, S11): the plan names each, the battle page
// declares each lazily, at the width a phone draws it, and the tree has one for every family a mutation names.
const read = path => readFileSync(new URL(`../${path}`, import.meta.url), 'utf8');
const families = new Set(Object.values(JSON.parse(read('data/presentation/mutation-art.json'))).map(visual => visual.family));
const scenes = [TREE_HEAD, RESULT_HEAD, ...Object.values(FAMILY_HEADS)];

test('the tree opens on the moult, each family column on its scene, and the result on the ground after Crushing Stomp', () => {
  assert.equal(TREE_HEAD.name, 'brute-moult');
  assert.equal(RESULT_HEAD.name, 'crushing-stomp-aftermath');
  assert.deepEqual(Object.fromEntries(Object.entries(FAMILY_HEADS).map(([family, { name }]) => [family, name])),
    { brute: 'ironhide-scar', predator: 'blightweaver-pressure-sac', warped: 'warped-listening' });
  assert.deepEqual([...families].sort((a, b) => a.localeCompare(b)), Object.keys(FAMILY_HEADS).sort((a, b) => a.localeCompare(b)), 'a family the data names has its head');
  // The words sit on the darker side: the moult's shell and print lie right, the stomp's slab and marks left.
  assert.equal(TREE_HEAD.side, 'start');
  assert.equal(RESULT_HEAD.side, 'end');
});

test('every scene of the match is declared lazily for the battle page, drawn at a width a phone sends', () => {
  for (const { name } of scenes) {
    const entry = PAGE_ART[name];
    assert.ok(entry, name);
    assert.equal(entry.pages[BATTLE_PAGE], 'lazy', name);
    assert.equal(entry.phone?.[BATTLE_PAGE]?.length, 1, name);
    assert.ok(entry.sizes.some(size => size.width === entry.phone[BATTLE_PAGE][0] * 2), `${name} is sent at twice its phone width`);
  }
  assert.match(FAMILY_HEAD_SIZES, /min\(94vw, 400px\)/, 'a column is min(94vw, 400px) wide on a phone (table/battle.css)');
});

test('the windows hold the words their heads wear, and the sheet sets the bands', () => {
  const page = read('table/battle.html'), sheet = read('table/battle.css');
  assert.match(page, /<header class="atlas-header"><div class="atlas-band"><div class="atlas-words">.*?<\/div><button id="atlas-close"[^>]*>Close<\/button><\/div><select id="atlas-creature"/);
  assert.match(page, /<header class="match-header"><span class="match-crest"[^>]*><\/span><div><p class="eyebrow" id="match-eyebrow">/);
  assert.match(sheet, /#atlas-board \.atlas-family-head\.scene-head::after \{ aspect-ratio: auto; height: 48px; max-height: none; \}/);
  assert.match(sheet, /#match-dialog \.match-header\.scene-head > \.match-crest \{ display: none; \}/);
});
