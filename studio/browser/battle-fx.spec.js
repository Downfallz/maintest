import { test, expect } from '@playwright/test';
import { IMPACT_MS, WINDUP_MS } from '../../table/battle-timing.js';
import { benchSamples } from '../../table/battle-bench.js';
import { SCENE_PARTICLES, stagingBeats } from '../../table/battle-staging.js';
import { openBench } from '../../scripts/battle/filmstrip.mjs';

// The effects kit (docs/art/battle-craft-plan.md, BC4; ADR 0196): each primitive played on the bench from its
// sample staging, on the film's clock. One Phaser page at a time, as the bench's spec.
test.describe.configure({ mode: 'default' });

const kit = page => page.evaluate(() => window.battleBench.kit());
const motifs = page => page.evaluate(() => window.battleBench.effects().map(one => `${one.motif}:${one.phase}`));
const film = (page, call, value) => page.evaluate(([name, argument]) => window.battleBench.film[name](argument), [call, value]);
// What a primitive holds while it plays: an object of its own, or a change to a figure or to the view.
const CHANGES = ['flash', 'pulse'];
const shaking = page => page.evaluate(() => window.battleBench.shaking());
const samples = benchSamples();
// One sample a primitive, and the one that asks past the field's budget (#882).
const primitives = samples.filter(one => one.id !== 'kit:budget');

test.beforeEach(async ({ page }) => { await openBench(page, '/table/battle-lab.html'); });

test('the kit arrives after the first frame, as a file of its own, and the bench lists its samples apart from the spells', async ({ page }) => {
  await expect(page.locator('#lab')).toHaveAttribute('data-kit', 'ready');
  const fetched = await page.evaluate(() => performance.getEntriesByType('resource').map(one => new URL(one.name).pathname + new URL(one.name).search));
  expect(fetched.filter(one => /\/battle-build\/fx\.js\?v=[0-9a-f]{12}$/.test(one))).toHaveLength(1);
  const listed = await page.locator('#bench-spell optgroup[label="The effects kit"] option').evaluateAll(options => options.map(option => option.value));
  expect(listed).toEqual(samples.map(one => one.id));
  expect(await kit(page)).toEqual({ objects: [], particles: 0, changes: 0 });
});

for (const sample of primitives) {
  const name = sample.id.split(':')[1];
  const beat = stagingBeats(sample).find(one => one.beat === name);

  test(`${name} plays at its beat, and a stop leaves nothing of it`, async ({ page }) => {
    await film(page, 'start', { spell: sample.id, actor: 'a1', targets: ['b1'] });
    // Before its beat the primitive has made nothing.
    if (beat.at > 40) {
      await film(page, 'advance', beat.at - 40);
      expect(await kit(page)).toEqual({ objects: [], particles: 0, changes: 0 });
      await film(page, 'advance', 80);
    } else await film(page, 'advance', 40);
    const playing = await kit(page);
    if (CHANGES.includes(name)) expect(playing.changes).toBe(1);
    // The shake is the view's: the kit adds to the scene's trauma and holds nothing of its own.
    else if (name === 'shake') expect(await shaking(page)).toBe(true);
    // The glow's sample opens its light and an aimed ring on one beat: each of its objects is the beat's.
    else {
      expect(playing.objects.length).toBeGreaterThan(0);
      for (const one of playing.objects) expect(one).toEqual({ beat: name, creature: beat.on === 'caster' ? 'a1' : 'b1' });
    }
    // A staged spell draws no shared charge on its caster: it gathers as its beats say.
    if (beat.window === 'windup') expect((await motifs(page)).filter(one => one.endsWith(':windup') && !one.startsWith('quill'))).toEqual([]);
    // Stopping is the path a skip, a rotation and a hidden tab take: every object destroyed, every figure put back.
    await page.evaluate(() => window.battleBench.stop());
    expect(await kit(page)).toEqual({ objects: [], particles: 0, changes: 0 });
    expect(await motifs(page)).toEqual([]);
    expect(await shaking(page)).toBe(false);
    await film(page, 'stop');
  });

  test(`${name} ends by itself inside the action`, async ({ page }) => {
    // A filter is drawn slowly by a renderer with no graphics card: the pulse's film takes several seconds.
    if (name === 'pulse') test.slow();
    await film(page, 'start', { spell: sample.id, actor: 'a1', targets: ['b1'] });
    await film(page, 'advance', WINDUP_MS + IMPACT_MS);
    expect(await kit(page)).toEqual({ objects: [], particles: 0, changes: 0 });
    await film(page, 'stop');
  });
}

test('a glow with the shape of a ring is one more object of the kit, gone when its beat ends', async ({ page }) => {
  await film(page, 'start', { spell: 'kit:glow', actor: 'a1', targets: ['b1'] });
  await film(page, 'advance', 960);
  // The soft light and the ring aimed along the blow, both from 840 ms.
  expect((await kit(page)).objects).toEqual([{ beat: 'glow', creature: 'b1' }, { beat: 'glow', creature: 'b1' }]);
  // The ring all around opens at 1000 ms, while the soft light, 500 ms long from 840, is still there.
  await film(page, 'advance', 80);
  expect((await kit(page)).objects).toHaveLength(3);
  // The last ring's 400 ms end at 1400 ms: one step later nothing is left.
  await film(page, 'advance', 380);
  expect((await kit(page)).objects).toEqual([]);
  await film(page, 'stop');
});

test('a staged motif is drawn at its beat and for as long as the beat says', async ({ page }) => {
  // The burst's sample places its motif at the contact, for 700 ms: gone well before the 1200 ms it had.
  await film(page, 'start', { spell: 'kit:burst', actor: 'a1', targets: ['b1'] });
  await film(page, 'advance', WINDUP_MS - 20);
  expect(await motifs(page)).toEqual([]);
  await film(page, 'advance', 40);
  expect(await motifs(page)).toEqual(['impact:impact']);
  await film(page, 'advance', 600);
  expect(await motifs(page)).toEqual(['impact:impact']);
  await film(page, 'advance', 140);
  expect(await motifs(page)).toEqual([]);
  await film(page, 'stop');
});

test('a motif shorter than its usual arrival is still gone when its beat ends', async ({ page }) => {
  // The flash's sample gives its motif 120 ms, less than the 200 ms a motif takes to arrive.
  await film(page, 'start', { spell: 'kit:flash', actor: 'a1', targets: ['b1'] });
  await film(page, 'advance', WINDUP_MS + 100);
  expect(await motifs(page)).toEqual(['impact:impact']);
  await film(page, 'advance', 60);
  expect(await motifs(page)).toEqual([]);
  await film(page, 'stop');
});

test('a staged shake adds to the view\'s one trauma at its beat, and the trauma falls back by itself', async ({ page }) => {
  await film(page, 'start', { spell: 'kit:shake', actor: 'a1', targets: ['b1'] });
  // The sample shakes 60 ms after the contact, and the bench told no loss: until then the view is still.
  await film(page, 'advance', WINDUP_MS + 40);
  expect(await shaking(page)).toBe(false);
  await film(page, 'advance', 40);
  expect(await shaking(page)).toBe(true);
  await film(page, 'advance', 300);
  expect(await shaking(page)).toBe(false);
  expect((await kit(page)).changes).toBe(0);
  await film(page, 'stop');
});

test('particles are counted while a burst flies, and never more than the beat asked for', async ({ page }) => {
  await film(page, 'start', { spell: 'kit:burst', actor: 'a1', targets: ['b1', 'b2', 'b3'] });
  // The copies of a beat are staggered, 40 ms a target: by 120 ms the third has started and the first still flies.
  await film(page, 'advance', WINDUP_MS + 40);
  expect((await kit(page)).objects.map(one => one.creature)).toEqual(['b1']);
  await film(page, 'advance', 80);
  const playing = await kit(page);
  // One burst a target, eight chips each.
  expect(playing.objects.map(one => one.creature)).toEqual(['b1', 'b2', 'b3']);
  expect(playing.particles).toBe(24);
  await film(page, 'stop');
});

// #882: the kit counts what each living stream may hold, not what it holds yet. Streams that start together each
// found the field empty when it counted particles alive, and passed the cap between them.
test('streams that start together never hold more than the field\'s budget of particles', async ({ page }) => {
  test.slow();
  await film(page, 'start', { spell: 'kit:budget', actor: 'a1', targets: ['b1', 'b2', 'b3'] });
  await film(page, 'advance', WINDUP_MS);
  let most = 0;
  for (let step = 0; step < IMPACT_MS / 40; step++) {
    await film(page, 'advance', 40);
    const { particles } = await kit(page);
    expect(particles).toBeLessThanOrEqual(SCENE_PARTICLES);
    most = Math.max(most, particles);
  }
  // The streams filled the field: the budget held them, not a quiet staging.
  expect(most).toBeGreaterThan(SCENE_PARTICLES * .8);
  await film(page, 'stop');
});

test('with less motion a staged spell plays nothing: no beat, no motif', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  // The bench rests when the media query's change event arrives, one rendering step after the emulation. A play
  // started before that was put back to its decision frame on a loaded runner (#894). The rest is what marks the
  // bench as reduced: once the attribute shows it, no change event is left to undo a play.
  await expect(page.locator('#lab')).toHaveAttribute('data-motion', 'reduced');
  for (const sample of samples) {
    await page.evaluate(id => { window.battleBench.choose({ spell: id, actor: 'a1', targets: ['b1'] }); window.battleBench.play(); }, sample.id);
    await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'resolved');
    expect(await kit(page)).toEqual({ objects: [], particles: 0, changes: 0 });
    expect(await motifs(page)).toEqual([]);
  }
});

test('a spell with no staging is drawn as before the kit: its charge, then its motif', async ({ page }) => {
  await film(page, 'start', { spell: 'spell:engulfing_flames:v1', actor: 'a1', targets: ['b1'] });
  expect(await motifs(page)).toEqual(['fire:windup']);
  await film(page, 'advance', WINDUP_MS);
  expect(await motifs(page)).toEqual(['fire:impact']);
  expect(await kit(page)).toEqual({ objects: [], particles: 0, changes: 0 });
  await film(page, 'stop');
});

// #918: a beat says where it starts on its figure, and what lies on the ground keeps to it.
const places = page => page.evaluate(() => window.battleBench.places());
const anchorOf = async (page, id) => (await page.evaluate(() => window.battleBench.anchors())).find(one => one.id === id);

test('a gather at the feet lands on the ground under its figure, below its centre', async ({ page }) => {
  await film(page, 'start', { spell: 'kit:gather', actor: 'a1', targets: ['b1'] });
  await film(page, 'advance', 300);
  const [gather] = (await places(page)).filter(one => one.beat === 'gather');
  const a1 = await anchorOf(page, 'a1');
  expect(a1.feet).toBeGreaterThan(a1.y);
  expect(a1.head).toBeLessThan(a1.y);
  expect(gather.x).toBeCloseTo(a1.x, 0);
  expect(gather.y).toBeCloseTo(a1.feet, 0);
  await film(page, 'stop');
});

test('a ring aimed along the blow opens on the target\'s side, at its feet', async ({ page }) => {
  await film(page, 'start', { spell: 'kit:glow', actor: 'a1', targets: ['b1'] });
  await film(page, 'advance', 900);
  const [ring] = (await places(page)).filter(one => one.facing !== undefined);
  const [a1, b1] = [await anchorOf(page, 'a1'), await anchorOf(page, 'b1')];
  expect(ring.creature).toBe('b1');
  expect(ring.y).toBeCloseTo(b1.feet, 0);
  // Its middle faces the way the blow went, from a1 to b1.
  expect(ring.facing).toBeCloseTo(Math.atan2(b1.y - a1.y, b1.x - a1.x) * 180 / Math.PI, 0);
  await film(page, 'stop');
});

test('a ring aimed at no one, a cast on its own caster, opens all around', async ({ page }) => {
  // Alpha Roar aims its three rings at the ally; cast on itself, the blow has no direction (#918 review).
  await film(page, 'start', { spell: 'spell:thundering_seal:v1', actor: 'a1', targets: ['a1'] });
  await film(page, 'advance', 900);
  const rings = (await places(page)).filter(one => one.beat === 'glow' && one.creature === 'a1');
  expect(rings.length).toBeGreaterThan(0);
  for (const ring of rings) expect([ring.kind, ring.facing]).toEqual(['Image', undefined]);
  await film(page, 'stop');
});

test('the caster\'s own wound goes with its body, and comes home with it', async ({ page }) => {
  // Wild Swing's caster pays in blood while it is at the target: the wound is drawn on the body, wherever it is.
  await film(page, 'start', { spell: 'spell:reckless_swing:v1', actor: 'a1', targets: ['b1'], wound: true });
  const wound = async () => (await page.evaluate(() => window.battleBench.effects())).find(one => one.motif === 'wound');
  const body = async () => (await page.evaluate(() => window.battleBench.motion())).figures.find(one => one.id === 'a1');
  await film(page, 'advance', WINDUP_MS + 120);
  const [away, there] = [await wound(), await body()];
  await film(page, 'advance', 900);
  const [home, back] = [await wound(), await body()];
  // On a phone the bench's blow is struck straight up the field: the body is carried along y.
  expect(Math.hypot(there.x, there.y)).toBeGreaterThan(20);
  expect(Math.hypot(back.x, back.y)).toBeLessThan(1);
  expect(away.x - home.x).toBeCloseTo(there.x - back.x, 0);
  expect(away.y - home.y).toBeCloseTo(there.y - back.y, 0);
  await film(page, 'stop');
});

test('a pulse on the acting caster quiets its gold rim while it swells, and gives it back', async ({ page }) => {
  // Crushing Stomp's bone pulse on its caster, in the windup.
  test.slow();
  await film(page, 'start', { spell: 'spell:crushing_stomp:v1', actor: 'a1', targets: ['b1'] });
  expect(await page.evaluate(() => window.battleBench.halo())).toEqual({ id: 'a1', quiet: false });
  await film(page, 'advance', 600);
  expect(await page.evaluate(() => window.battleBench.halo())).toEqual({ id: 'a1', quiet: true });
  await film(page, 'advance', WINDUP_MS + IMPACT_MS - 600);
  expect(await page.evaluate(() => window.battleBench.halo())).toEqual({ id: 'a1', quiet: false });
  await film(page, 'stop');
});

test('a slash cuts across the blow, where an impact\'s strokes run along it', async ({ page }) => {
  const strokes = async spell => {
    await film(page, 'start', { spell, actor: 'a1', targets: ['b1'] });
    await film(page, 'advance', WINDUP_MS + 100);
    const motif = (await page.evaluate(() => window.battleBench.effects())).find(one => one.phase === 'impact' && one.strokes !== undefined);
    await film(page, 'stop');
    return motif.strokes;
  };
  const [a1, b1] = [await anchorOf(page, 'a1'), await anchorOf(page, 'b1')];
  const blow = Math.atan2(b1.y - a1.y, b1.x - a1.x) * 180 / Math.PI;
  // A stroke is a line: its angle is read modulo 180°.
  const apart = angle => { const turn = ((angle - blow) % 180 + 180) % 180; return Math.min(turn, 180 - turn); };
  expect(apart(await strokes('spell:pummel:v1'))).toBeLessThan(2);
  expect(apart(await strokes('spell:reckless_swing:v1'))).toBeGreaterThan(45);
});
