// The ground's numbers (docs/art/storyboards/ground.md, BC9): the air from the ambient clock alone, the depth of
// each layer, and the light that answers the health the host confirmed lost.

import { test } from 'node:test';
import assert from 'node:assert/strict';

import { DEPTH, EMBERS, EMBER_COUNT, FOG, LIGHT, LIGHT_COLOURS, PAINTING, REST_TINT, coronaScale, depthPlace, depthShift, emberAt, emberGlow, fogDrift, lightAt, lightOf, mix } from './battle-ground.js';
import { TRAUMA, PUNCH } from './battle-motion.js';
import { IMPACT_MS } from './battle-timing.js';

// The bench's field: 390 by 320, the painting laid at 0.34 of its size from the top.
const field = { width: 390, height: 320, top: 0, scale: .34 };
const cue = (family, changes, extra = {}) => ({ kind: 'strike', visual: { family }, recipients: changes.map(one => one.creature), changes, ...extra });

test('the fog drifts eight pixels a second on the ambient clock, and stands still with less motion', () => {
  assert.equal(fogDrift(0), 0);
  assert.ok(Math.abs(fogDrift(11000) - 88) < 1e-9);
  assert.equal(fogDrift(11000, true), 0);
  // One texture, tileable across: a puff that crosses an edge is drawn again on the other side by the scene.
  assert.equal(FOG.puffs.length, 7);
  for (const [x, y, across, down, alpha] of FOG.puffs) {
    assert.ok(x >= 0 && x < FOG.tile.width && y - down >= 0 && y + down <= FOG.tile.height, 'a puff stays inside the band\'s height');
    assert.ok(across > 0 && alpha > 0 && alpha <= 1);
  }
});

test('an ember is where the ambient clock says, the same each time it is asked', () => {
  assert.equal(EMBER_COUNT, 24);
  for (let index = 0; index < EMBER_COUNT; index++) assert.deepEqual(emberAt(index, 4321, field), emberAt(index, 4321, field));
});

test('no ember rises over the sky, and none appears or vanishes at once', () => {
  for (let index = 0; index < EMBER_COUNT; index++) {
    const kind = index < EMBERS.far.count ? EMBERS.far : EMBERS.near;
    let last = null;
    for (let clock = 0; clock <= 60000; clock += 100) {
      const ember = emberAt(index, clock, field);
      assert.equal(ember.far, index < EMBERS.far.count);
      assert.ok(ember.y <= kind.rows[0] * field.scale + 1e-9 && ember.y >= kind.rows[1] * field.scale - 1e-9, `ember ${index} stays between its rows`);
      assert.ok(ember.y >= 420 * field.scale - 1e-9, 'under the horizon');
      assert.ok(ember.alpha >= 0 && ember.alpha <= kind.alpha);
      // Where its rise starts again it is at no alpha: the jump back down is never seen.
      if (last && ember.y > last.y + 1) assert.ok(ember.alpha < .02 && last.alpha < .02, `ember ${index} wraps unseen`);
      if (last) assert.ok(Math.abs(ember.x - last.x) < 1, 'it sways, it does not jump across');
      last = ember;
    }
  }
});

test('a far ember is smaller, dimmer and slower than a near one', () => {
  assert.ok(EMBERS.far.size < EMBERS.near.size && EMBERS.far.alpha < EMBERS.near.alpha && EMBERS.far.speed < EMBERS.near.speed);
  // Over a second, at the middle of its rise: 1.2% and 2.5% of the field's height.
  const rise = index => { for (let clock = 0; ; clock += 50) { const a = emberAt(index, clock, field), b = emberAt(index, clock + 1000, field); if (b.y < a.y) return a.y - b.y; } };
  assert.ok(Math.abs(rise(0) - EMBERS.far.speed * field.height) < .01);
  assert.ok(Math.abs(rise(EMBERS.far.count) - EMBERS.near.speed * field.height) < .01);
  // A taller field does not make an ember faster for its size.
  const tall = { ...field, height: 640, scale: .68 };
  const risen = emberAt(0, 0, tall).y - emberAt(0, 1000, tall).y;
  if (risen > 0) assert.ok(Math.abs(risen - EMBERS.far.speed * tall.height) < .01);
});

test('the embers brighten as the rounds pass, from 0.8 at the first to 1 from the twelfth', () => {
  assert.deepEqual([null, 1, 12, 30].map(emberGlow), [.8, .8, 1, 1]);
  assert.ok(emberGlow(6) > emberGlow(1) && emberGlow(6) < emberGlow(12));
});

test('the embers are not the target marker\'s gold, and the fog is no family\'s colour', () => {
  assert.notEqual(EMBERS.tint, 0xeac88e);
  assert.ok(!Object.values(LIGHT_COLOURS).includes(FOG.tint));
});

test('a layer on the floor moves with the view, and a far one by its factor', () => {
  for (const layer of ['painting', 'floor', 'arch', 'near']) assert.deepEqual(depthShift(DEPTH[layer], { x: 3, y: -2, zoom: 1.03 }), { x: 0, y: 0, scale: 1 });
  const sky = depthShift(DEPTH.sky, { x: 3, y: -2, zoom: 1 });
  assert.deepEqual(sky, { x: 1.5, y: -1, scale: 1 });
  assert.ok(DEPTH.sky < DEPTH.fog && DEPTH.fog < DEPTH.far && DEPTH.far < DEPTH.near);
  // The view at rest leaves every layer where it lies.
  for (const factor of Object.values(DEPTH)) assert.deepEqual(depthShift(factor, { x: 0, y: 0, zoom: 1 }), { x: 0, y: 0, scale: 1 });
});

test('under the punch a far layer zooms by its factor of it, about the centre of the view', () => {
  const zoom = 1 + PUNCH.amount, centre = { x: 195, y: 160 }, point = { x: 195, y: 0 };
  const placed = depthPlace(point, DEPTH.sky, { x: 0, y: 0, zoom }, centre);
  // On the screen: the camera's zoom times where it was laid, from the centre.
  const seen = centre.y + zoom * (placed.y - centre.y);
  assert.ok(Math.abs(seen - (centre.y + (1 + DEPTH.sky * PUNCH.amount) * (point.y - centre.y))) < 1e-9);
  assert.ok(Math.abs(placed.scale * zoom - 1.015) < 1e-9);
  // The floor's own point is left to the camera.
  assert.deepEqual(depthPlace(point, DEPTH.floor, { x: 2, y: 1, zoom }, centre), { ...point, scale: 1 });
  // What the storyboard promises at the shake's ceiling on the bench's field: the sky a pixel and a half behind the floor.
  const reach = TRAUMA.ceiling ** 2 * TRAUMA.reach * 320;
  assert.ok(Math.abs(depthShift(DEPTH.sky, { x: reach, y: 0, zoom: 1 }).x - reach / 2) < 1e-9 && reach / 2 < 3);
});

test('the ground answers harm by its weight: nothing to 3 health lost, half from 4, all from 7 or on a defeat', () => {
  const lost = health => lightOf(cue('elemental', [{ creature: 'b1', health: -health }]), 'a1').strength;
  assert.deepEqual([0, 3, 4, 6, 7, 11].map(lost), [0, 0, .5, .5, 1, 1]);
  assert.equal(lightOf(cue('physical', [{ creature: 'b1', health: -1, defeated: true }]), 'a1').strength, 1);
  // The health lost by the others is added up: three figures that lose 3 each are a heavy blow to the field.
  assert.equal(lightOf(cue('elemental', [{ creature: 'b1', health: -3 }, { creature: 'b2', health: -3 }, { creature: 'b3', health: -3 }]), 'a1').strength, 1);
  assert.deepEqual(lightOf(cue('elemental', [{ creature: 'b1', health: -5 }]), 'a1'), { strength: .5, colour: 0x8dcfe5, under: ['b1'] });
  // The light also pools under each figure that lost health, and under no other: not the caster, not one that only received.
  const many = cue('physical', [{ creature: 'b1', health: -6 }, { creature: 'b2', health: 2 }, { creature: 'a1', health: -3 }, { creature: 4, defeated: true }]);
  assert.deepEqual(lightOf({ ...many, recipients: ['b1', 'b2', 'b3', 'a1', 4] }, 'a1').under, ['b1', '4']);
});

test('a heal, a ward, a blood cost alone, a fizzle and an action of no family leave the ground as it rests', () => {
  const none = { strength: 0, colour: null, under: [] };
  assert.deepEqual(lightOf(cue('astral', [{ creature: 'a2', health: 8 }]), 'a1'), none);
  assert.deepEqual(lightOf(cue('necromantic', []), 'a1'), none);
  assert.deepEqual(lightOf(cue('necromantic', [{ creature: 'a1', health: -9 }]), 'a1'), none);
  assert.deepEqual(lightOf({ ...cue('physical', [{ creature: 'b1', health: -9 }]), kind: 'fizzle' }, 'a1'), none);
  assert.deepEqual(lightOf(cue('neutral', [{ creature: 'b1', health: -9 }]), 'a1'), none);
  assert.deepEqual(lightOf(cue('constructor', [{ creature: 'b1', health: -9 }]), 'a1'), none);
  assert.deepEqual(lightOf(undefined, 'a1'), none);
  // The host's ids may be numbers: the caster is still told from the others.
  assert.deepEqual(lightOf(cue('necromantic', [{ creature: 4, health: -9 }]), '4'), none);
});

test('the embers are spread over the floor: no two of one kind close, no three in one row or one column', () => {
  // The first two films strung them on lines: a constant step for the column and the rise makes a lattice.
  for (const [from, count] of [[0, EMBERS.far.count], [EMBERS.far.count, EMBERS.near.count]]) {
    const kind = Array.from({ length: count }, (_, index) => emberAt(from + index, 0, field));
    for (const [at, one] of kind.entries()) for (const other of kind.slice(at + 1)) {
      assert.ok(Math.hypot(one.x - other.x, one.y - other.y) >= 20, 'two embers of one kind stand apart');
    }
    for (const axis of ['x', 'y']) {
      const along = kind.map(one => one[axis]).sort((a, b) => a - b);
      for (let at = 0; at + 2 < along.length; at++) assert.ok(along[at + 2] - along[at] >= 3, `no three share one ${axis === 'x' ? 'column' : 'row'}`);
    }
  }
});

test('the corona is laid larger once the sky has shattered, so it stays off the disc\'s two halves', () => {
  assert.deepEqual([null, 1, 9, 10, 12, 30].map(coronaScale), [1, 1, 1, 1.5, 1.5, 1.5]);
});

test('the light is one rise and one fall, gone within 320 ms of the contact, and never the whole way to a colour', () => {
  assert.ok(LIGHT.rise + LIGHT.fall <= 320);
  assert.ok(LIGHT.rise + LIGHT.fall < IMPACT_MS);
  assert.ok(LIGHT.tint <= .5 && LIGHT.alpha <= 1 && LIGHT.under.alpha <= 1 && LIGHT.under.flat < 1);
  assert.deepEqual([-20, 0, LIGHT.rise + LIGHT.fall, 5000].map(lightAt), [0, 0, 0, 0]);
  assert.equal(lightAt(LIGHT.rise), 1);
  let peaked = false;
  for (let ms = 20; ms < LIGHT.rise + LIGHT.fall; ms += 20) {
    const now = lightAt(ms), before = lightAt(ms - 20);
    assert.ok(now > 0 && now <= 1);
    if (now < before) peaked = true;
    assert.ok(peaked ? now < before : now > before, `one rise, then one fall, at ${ms} ms`);
  }
  // The corona is around the disc and leaves it dark.
  assert.ok(LIGHT.ring.inner < 1 && LIGHT.ring.inner < LIGHT.ring.peak && LIGHT.ring.peak < LIGHT.ring.outer);
  assert.equal(mix(REST_TINT.painting, LIGHT_COLOURS.elemental, 0), REST_TINT.painting);
  assert.equal(mix(REST_TINT.painting, LIGHT_COLOURS.elemental, 1), LIGHT_COLOURS.elemental);
  assert.equal(mix(0x000000, 0xff8040, .5), 0x804020);
  assert.equal(mix(0x102030, 0xffffff, 7), 0xffffff);
  // No family's colour is the target marker's gold.
  assert.ok(!Object.values(LIGHT_COLOURS).includes(0xeac88e));
  assert.ok(PAINTING.eclipse.y < 420);
});
