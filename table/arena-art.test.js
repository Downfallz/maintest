import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { CREATURE_ART, creatureForm, figureArt, figureGeometry } from './arena-art.js';
import { placements } from './arena-layout.js';
import { livePresentation } from './arena-live.js';
import { presentedView } from './presentation.js';

const brute = 'tier:brute:v1', predator = 'tier:prowler:v1', warped = 'tier:occultist:v1';

test('the acquired root, not name, seat or roster order, determines a single-family form', () => {
  for (const [root, form] of [[brute, 'brute'], [predator, 'predator'], [warped, 'warped']]) {
    const one = { id: 8, name: 'Misleading name', acquiredTiers: [root, 'tier:later:v1', root] };
    assert.equal(creatureForm(one), form);
    assert.equal(livePresentation({ board: { allies: [one] } }).creatures[0].form, form);
    assert.equal(livePresentation({ board: { enemies: [one] } }).creatures[0].form, form);
  }
});

test('unmutated, legacy, unknown and mixed-family creatures use the neutral ancestor', () => {
  for (const acquiredTiers of [undefined, [], ['future'], [brute, predator], [warped, brute], [predator, warped, brute]]) {
    assert.equal(creatureForm({ acquiredTiers }), 'base-creature');
    if (acquiredTiers) assert.equal(creatureForm({ acquiredTiers: [...acquiredTiers].reverse() }), 'base-creature');
  }
});

test('historical forms come from the displayed frame, never the latest mutations', () => {
  const one = { id: 1, acquiredTiers: [warped] };
  const view = { board: { allies: [one], enemies: [] } };
  const action = { actor: { id: 1 }, round: 1, frame: { before: [{ ...one, acquiredTiers: [] }], after: [{ ...one, acquiredTiers: [brute] }] } };
  for (const [stage, form] of [['before', 'base-creature'], ['after', 'brute']]) {
    assert.equal(livePresentation(presentedView(view, { actions: [action], index: 0, stage })).creatures[0].form, form);
  }
  assert.equal(livePresentation(view).creatures[0].form, 'warped');
});

test('a missing family image falls back to the ancestor, then the selectable vector', () => {
  assert.equal(figureArt('brute', () => true).form, 'brute');
  assert.equal(figureArt('unknown', () => true).form, 'base-creature');
  assert.equal(figureArt('brute', key => key === 'creature-base-creature').form, 'base-creature');
  assert.equal(figureArt('brute', () => false).key, 'missing-figure');
});

test('runtime WebPs preserve the audited ART-2 bytes, dimensions and bounds', () => {
  const inventory = JSON.parse(readFileSync(new URL('../docs/art/source-intake.json', import.meta.url)));
  for (const art of CREATURE_ART) {
    const source = inventory.assets.find(one => one.form === art.form);
    const bytes = readFileSync(new URL(`arena-art/${art.file}`, import.meta.url));
    assert.equal(bytes.length, source.referenceWebp.sizeBytes);
    assert.equal(createHash('sha256').update(bytes).digest('hex'), source.referenceWebp.sha256);
    assert.deepEqual(art.bounds, source.alpha.boundsAtLeast['128']);
    assert.deepEqual([art.width, art.height], [source.width, source.height]);
  }
});

test('both facing directions keep silhouette and contact point inside shared hit targets', () => {
  for (const [width, height] of [[1440, 700], [844, 206], [390, 550], [320, 280]]) {
    for (const home of placements(width, height)) for (const art of CREATURE_ART) for (const enemy of [true, false]) {
      const g = figureGeometry(art, home, enemy);
      const [left, top, right, bottom] = art.bounds;
      const anchorX = g.originX * art.width, anchorY = g.originY * art.height;
      const x1 = enemy ? art.width - right : left, x2 = enemy ? art.width - left : right;
      assert.ok((x1 - anchorX) * g.scale >= -home.hitWidth / 2 - 1e-8);
      assert.ok((x2 - anchorX) * g.scale <= home.hitWidth / 2 + 1e-8);
      assert.ok((top - anchorY) * g.scale >= -home.size - 1e-8);
      assert.equal((bottom - anchorY) * g.scale, 0);
      assert.ok(Number.isFinite(g.scale) && g.scale > 0);
    }
  }
});
