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
// One sample a primitive, the one that asks past the field's budget (#882), and the one with no staging at all.
const primitives = samples.filter(one => !['kit:budget', 'kit:plain'].includes(one.id));

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
    // So is the push: the view has started to move in, and the kit holds nothing.
    else if (name === 'push') expect((await page.evaluate(() => window.battleBench.motion())).view.zoom).toBeGreaterThan(1);
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
    expect((await page.evaluate(() => window.battleBench.motion())).view.zoom).toBe(1);
    await film(page, 'stop');
  });

  test(`${name} ends by itself inside the action`, async ({ page }) => {
    // A filter is drawn slowly by a renderer with no graphics card: the pulse's film takes several seconds.
    if (name === 'pulse') test.slow();
    await film(page, 'start', { spell: sample.id, actor: 'a1', targets: ['b1'] });
    await film(page, 'advance', WINDUP_MS + IMPACT_MS);
    expect(await kit(page)).toEqual({ objects: [], particles: 0, changes: 0 });
    expect((await page.evaluate(() => window.battleBench.motion())).view.zoom).toBe(1);
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
  // Every spell is staged since BC8: the bench keeps one sample with no staging for this.
  await film(page, 'start', { spell: 'kit:plain', actor: 'a1', targets: ['b1'] });
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
  expect(await page.evaluate(() => window.battleBench.halo())).toEqual({ id: 'a1', quiet: false, focusQuiet: [] });
  await film(page, 'advance', 600);
  expect(await page.evaluate(() => window.battleBench.halo())).toEqual({ id: 'a1', quiet: true, focusQuiet: [] });
  await film(page, 'advance', WINDUP_MS + IMPACT_MS - 600);
  expect(await page.evaluate(() => window.battleBench.halo())).toEqual({ id: 'a1', quiet: false, focusQuiet: [] });
  await film(page, 'stop');
});

test('a pulse on a figure lit blue quiets its blue rim too, and gives it back (#937)', async ({ page }) => {
  test.slow();
  // Crushing Stomp's bone pulse on its caster, a1, which the film lights blue as the turn order would.
  await film(page, 'start', { spell: 'spell:crushing_stomp:v1', actor: 'a1', targets: ['b1'], focus: ['a1'] });
  expect(await page.evaluate(() => window.battleBench.halo())).toEqual({ id: 'a1', quiet: false, focusQuiet: [] });
  await film(page, 'advance', 600);
  expect(await page.evaluate(() => window.battleBench.halo())).toEqual({ id: 'a1', quiet: true, focusQuiet: ['a1'] });
  await film(page, 'advance', WINDUP_MS + IMPACT_MS - 600);
  expect(await page.evaluate(() => window.battleBench.halo())).toEqual({ id: 'a1', quiet: false, focusQuiet: [] });
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

// Where a figure's body has been carried from its home now, as the bench's motion reads it.
const bodyOf = async (page, id) => (await page.evaluate(() => window.battleBench.motion())).figures.find(one => one.id === id);

test('a windup beat on a melee caster goes with its body when it leaves its place (#933)', async ({ page }) => {
  // Deranged Charge's ember light on its caster ends at 780 ms; the caster leaves its ring at about 720 ms.
  await film(page, 'start', { spell: 'spell:psycho_rush:v1', actor: 'a1', targets: ['b1'] });
  await film(page, 'advance', 760);
  const [light] = (await places(page)).filter(one => one.beat === 'glow' && one.creature === 'a1');
  const [a1, body] = [await anchorOf(page, 'a1'), await bodyOf(page, 'a1')];
  expect(Math.hypot(body.x, body.y)).toBeGreaterThan(20);
  expect(light.x - a1.x).toBeCloseTo(body.x, 0);
  expect(light.y - a1.y).toBeCloseTo(body.y, 0);
  await film(page, 'stop');
});

test('a spark points the way it flies: a guard on the order the engine reads a piece in (#936)', async ({ page }) => {
  await film(page, 'start', { spell: 'kit:burst', actor: 'a1', targets: ['b1'] });
  // On a phone the blow runs up the field: the pieces' travel lies on the ground, shortened, so they are read a
  // little later than on a desktop.
  await film(page, 'advance', 960);
  const [burst] = (await places(page)).filter(one => one.beat === 'burst');
  const flying = burst.pieces.filter(piece => Math.hypot(piece.x - burst.x, piece.y - burst.y) > 4);
  expect(flying.length).toBeGreaterThan(3);
  for (const piece of flying) {
    const travel = Math.atan2(piece.y - burst.y, piece.x - burst.x) * 180 / Math.PI;
    expect(Math.abs(((piece.angle - travel) % 360 + 540) % 360 - 180)).toBeLessThan(12);
  }
  await film(page, 'stop');
});

test('a gather closes on its figure\'s outline, not on its centre (#942)', async ({ page }) => {
  // Latch's six bone hooks land on b1 from 840 to 1140 ms.
  await film(page, 'start', { spell: 'spell:latch:v1', actor: 'a1', targets: ['b1'] });
  await film(page, 'advance', 1120);
  const [hooks] = (await places(page)).filter(one => one.beat === 'gather');
  const b1 = await anchorOf(page, 'b1');
  expect(hooks.pieces.length).toBeGreaterThan(0);
  for (const piece of hooks.pieces) expect(Math.hypot(piece.x - b1.x, piece.y - b1.y)).toBeGreaterThan(b1.radius * .4);
  await film(page, 'stop');
});

test('a lowered defense sheds small flakes of hide that fall, not chips thrown out like a hit (#942)', async ({ page }) => {
  // Toxic Mend's flakes, from 1240 ms, on the small figures of its three allies.
  await film(page, 'start', { spell: 'spell:noxious_cure:v1', actor: 'a1', targets: ['a1', 'a2', 'a3'] });
  await film(page, 'advance', 1440);
  const flakes = (await places(page)).filter(one => one.beat === 'burst');
  expect(flakes.length).toBeGreaterThan(0);
  for (const flake of flakes) {
    const { radius } = await anchorOf(page, flake.creature);
    for (const piece of flake.pieces) {
      expect(piece.y).toBeGreaterThan(flake.y);
      // A flake's frame is 32 px: drawn, it is well under half its figure's radius, a chip of hide and not a blow's.
      expect(piece.scale * 32).toBeLessThan(radius * .4);
    }
  }
  await film(page, 'stop');
});

test('the motif is drawn under the particles on its figure (#942)', async ({ page }) => {
  // Venom Claw's beads on b1, over the claw marks.
  await film(page, 'start', { spell: 'spell:poison_slash:v1', actor: 'a1', targets: ['b1'] });
  await film(page, 'advance', 1000);
  const motif = (await page.evaluate(() => window.battleBench.effects())).find(one => one.phase === 'impact');
  const kitObjects = await places(page);
  expect(kitObjects.length).toBeGreaterThan(0);
  for (const object of kitObjects) expect(object.depth).toBeGreaterThan(motif.depth);
  await film(page, 'stop');
});

test('a trail from a caster to an ally goes at an even pace: half way at half its beat (#947)', async ({ page }) => {
  // Revitalize's trail runs from a1 to a2 from 840 to 1200 ms.
  await film(page, 'start', { spell: 'spell:rejuvenate:v1', actor: 'a1', targets: ['a2'] });
  await film(page, 'advance', 1020);
  const [trail] = (await places(page)).filter(one => one.beat === 'trail');
  const [a1, a2] = [await anchorOf(page, 'a1'), await anchorOf(page, 'a2')];
  const share = Math.hypot(trail.x - a1.x, trail.y - a1.y) / Math.hypot(a2.x - a1.x, a2.y - a1.y);
  expect(share).toBeGreaterThan(.4);
  expect(share).toBeLessThan(.6);
  await film(page, 'stop');
});

test('a capstone pushes the view in while it gathers and lets go at the impact (#950)', async ({ page }) => {
  // Crushing Stomp's windup holds its pulse, a filter a renderer with no graphics card draws slowly, and the test
  // films it twice: on the desktop runner that took more than 30 s.
  test.slow();
  // Crushing Stomp, Stage III: no defeat and no critical hit, so no punch of the zoom.
  await film(page, 'start', { spell: 'spell:crushing_stomp:v1', actor: 'a1', targets: ['b1'] });
  const zoom = async () => (await page.evaluate(() => window.battleBench.motion())).view.zoom;
  expect(await zoom()).toBe(1);
  await film(page, 'advance', 760);
  const pushed = await zoom();
  expect(pushed).toBeGreaterThan(1.015);
  await film(page, 'advance', 600);
  expect(await zoom()).toBe(1);
  // Skipped mid-push, the view is put back as it was fitted.
  await film(page, 'stop');
  await film(page, 'start', { spell: 'spell:crushing_stomp:v1', actor: 'a1', targets: ['b1'] });
  await film(page, 'advance', 600);
  await page.evaluate(() => window.battleBench.stop());
  expect(await zoom()).toBe(1);
  await film(page, 'stop');
});
