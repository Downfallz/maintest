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
const CHANGES = ['flash', 'shake', 'pulse'];
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
    else expect(playing.objects).toEqual([{ beat: name, creature: beat.on === 'caster' ? 'a1' : 'b1' }]);
    // A staged spell draws no shared charge on its caster: it gathers as its beats say.
    if (beat.window === 'windup') expect((await motifs(page)).filter(one => one.endsWith(':windup') && !one.startsWith('quill'))).toEqual([]);
    // Stopping is the path a skip, a rotation and a hidden tab take: every object destroyed, every figure put back.
    await page.evaluate(() => window.battleBench.stop());
    expect(await kit(page)).toEqual({ objects: [], particles: 0, changes: 0 });
    expect(await motifs(page)).toEqual([]);
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

test('a staging that shakes the view shakes it once: the shared tremor of a physical blow is left out', async ({ page }) => {
  const shaking = () => page.evaluate(() => window.battleBench.shaking());
  await film(page, 'start', { spell: 'kit:shake', actor: 'a1', targets: ['b1'] });
  // The sample shakes 60 ms after the contact: until then the view is still, where the shared tremor started at once.
  await film(page, 'advance', WINDUP_MS + 40);
  expect(await shaking()).toBe(false);
  await film(page, 'advance', 60);
  expect(await shaking()).toBe(true);
  await film(page, 'advance', 240);
  expect(await shaking()).toBe(false);
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
