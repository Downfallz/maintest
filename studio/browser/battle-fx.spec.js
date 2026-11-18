import { test, expect } from '@playwright/test';
import { IMPACT_MS, WINDUP_MS } from '../../table/battle-timing.js';
import { benchSamples } from '../../table/battle-bench.js';
import { stagingBeats } from '../../table/battle-staging.js';
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

test.beforeEach(async ({ page }) => { await openBench(page, '/table/battle-lab.html'); });

test('the kit arrives after the first frame, as a file of its own, and the bench lists its samples apart from the spells', async ({ page }) => {
  await expect(page.locator('#lab')).toHaveAttribute('data-kit', 'ready');
  const fetched = await page.evaluate(() => performance.getEntriesByType('resource').map(one => new URL(one.name).pathname + new URL(one.name).search));
  expect(fetched.filter(one => /\/battle-build\/fx\.js\?v=[0-9a-f]{12}$/.test(one))).toHaveLength(1);
  const listed = await page.locator('#bench-spell optgroup[label="The effects kit"] option').evaluateAll(options => options.map(option => option.value));
  expect(listed).toEqual(samples.map(one => one.id));
  expect(await kit(page)).toEqual({ objects: [], particles: 0, changes: 0 });
});

for (const sample of samples) {
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
    await film(page, 'start', { spell: sample.id, actor: 'a1', targets: ['b1'] });
    await film(page, 'advance', WINDUP_MS + IMPACT_MS);
    expect(await kit(page)).toEqual({ objects: [], particles: 0, changes: 0 });
    await film(page, 'stop');
  });
}

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

test('with less motion a staged spell plays nothing: no beat, no motif', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
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
