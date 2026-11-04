import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { CREATURE_ART, MUTATION_ART, creatureAppearance, creatureForm, figureArt, figureGeometry } from './arena-art.js';
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

test('unmutated, legacy, malformed and orphaned mutations use the neutral ancestor', () => {
  for (const acquiredTiers of [undefined, [], ['future'], ['tier:ravager:v1'], ['__proto__', 'toString']]) {
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

const id = name => `tier:${name}:v1`;
const appearance = names => creatureAppearance({ acquiredTiers: names.map(id) });

test('the visual register covers the authored tiers with the correct stages', () => {
  const root = new URL('../data/Tiers/', import.meta.url);
  const tiers = readdirSync(root).filter(path => path.endsWith('.json')).map(path => JSON.parse(readFileSync(new URL(path, root))));
  assert.deepEqual(Object.keys(MUTATION_ART).sort(), tiers.map(tier => tier.id).sort());
  for (const tier of tiers) assert.equal(MUTATION_ART[tier.id].stage, tier.level);
});

test('multiclass body precedence uses stage, unique investment and a fixed tie order', () => {
  for (const [names, form] of [
    [['prowler', 'brute'], 'brute'], [['occultist', 'prowler'], 'predator'],
    [['brute', 'ironbound', 'prowler', 'assassin', 'deathstalker'], 'predator'],
    [['brute', 'ironbound', 'berserker', 'occultist', 'necromancer'], 'brute'],
    [['brute', 'ironbound', 'dreadnought', 'occultist', 'necromancer', 'lich', 'archmage'], 'warped'],
  ]) {
    const expected = appearance(names);
    assert.equal(expected.form, form);
    assert.deepEqual(appearance([...names].reverse()), expected);
    assert.deepEqual(appearance([...names, ...names]), expected);
  }
});

test('layers have a fixed budget and stage three replaces its stage two motif', () => {
  const result = appearance(['brute', 'berserker', 'ravager', 'ironbound', 'dreadnought',
    'prowler', 'plague_doctor', 'blightweaver', 'occultist', 'necromancer', 'lich', 'titan', 'apex', 'archmage']);
  assert.equal(result.form, 'brute');
  assert.deepEqual(result.layers.map(cue => cue.slot), ['body', 'accent', 'capstone']);
  assert.deepEqual(result.layers.map(cue => cue.motif), ['plates', 'glands', 'reinforcement']);
  assert.deepEqual(result.layers.map(cue => cue.stage), [3, 3, 4]);
  const two = appearance(['brute', 'ironbound']);
  const three = appearance(['brute', 'ironbound', 'dreadnought']);
  assert.equal(two.layers.length, 1); assert.equal(three.layers.length, 1);
  assert.equal(two.layers[0].stage, 2); assert.equal(three.layers[0].stage, 3);
});

test('same-family branches share the bounded accent slot and root-only mixes add no anatomy', () => {
  assert.deepEqual(appearance(['brute', 'berserker', 'ironbound']).layers.map(cue => cue.motif), ['fissures', 'plates']);
  assert.deepEqual(appearance(['brute', 'prowler', 'occultist']).layers, []);
});

test('capstones stay on their owner and an unknown or inherited passive cannot invent a layer', () => {
  const own = { acquiredTiers: ['occultist', 'necromancer', 'lich', 'archmage'].map(id) };
  const ally = { acquiredTiers: ['brute'].map(id), sharedTiers: [id('archmage')], upkeepEnergy: 3 };
  const view = livePresentation({ board: { allies: [own, ally] } });
  assert.ok(view.creatures[0].layers.some(cue => cue.motif === 'circuit'));
  assert.equal(view.creatures[1].layers.length, 0);
  assert.deepEqual(creatureAppearance({ acquiredTiers: 'invalid' }).layers, []);
  assert.deepEqual(appearance(['brute', 'future']), appearance(['brute']));
});

test('historical layers use only the displayed snapshot without sorting or changing host data', () => {
  const old = { id: 1, acquiredTiers: ['brute', 'ironbound'].map(id) };
  const latest = { ...old, acquiredTiers: ['brute', 'ironbound', 'dreadnought', 'titan'].map(id) };
  const view = { board: { allies: [latest], enemies: [] } };
  const original = structuredClone(view);
  const action = { actor: { id: 1 }, round: 1, frame: { before: [old], after: [latest] } };
  assert.equal(livePresentation(presentedView(view, { actions: [action], index: 0, stage: 'before' })).creatures[0].layers[0].stage, 2);
  assert.deepEqual(livePresentation(presentedView(view, { actions: [action], index: 0, stage: 'after' })).creatures[0].layers.map(cue => cue.stage), [3, 4]);
  assert.deepEqual(view, original);
});
