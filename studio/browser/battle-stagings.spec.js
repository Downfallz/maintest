import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { test, expect } from '@playwright/test';
import { IMPACT_MS, WINDUP_MS } from '../../table/battle-timing.js';
import { SCENE_PARTICLES, stagingBeats } from '../../table/battle-staging.js';
import { benchDuration, FILM_TICK_MS } from '../../table/battle-bench.js';
import { castTargets } from '../../scripts/battle/cast.mjs';
import { openBench } from '../../scripts/battle/filmstrip.mjs';
import { measureFrames, measureSteps } from '../../scripts/battle/frametime.mjs';

// The stagings the spell art carries (docs/art/battle-craft-plan.md, BC6 to BC8): each staged spell is played on
// the bench, on the film's clock, as the content says it is cast: on as many figures as it may reach. Each beat
// it holds is seen alive, the field never holds more particles than its budget, and nothing is left once the
// action has resolved. One Phaser page at a time, as the bench's spec.
test.describe.configure({ mode: 'default' });

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const art = JSON.parse(readFileSync(join(root, 'data/presentation/spell-art.json'), 'utf8'));
// Who the bench plays a spell on: the caster itself, one ally, or as many enemies as the spell may reach.
const targetsOf = castTargets(root);

const staged = Object.entries(art).filter(([, visual]) => visual.staging)
  .map(([id, visual]) => ({ id, beats: stagingBeats(visual), targets: targetsOf(id) }));
// The beats that hold an object of the kit while they play; a flash and a pulse change a figure, a shake the view.
const OBJECTS = ['burst', 'flow', 'trail', 'gather', 'glow'];
const STEP_MS = 60;

const kit = page => page.evaluate(() => window.battleBench.kit());
const motifs = page => page.evaluate(() => window.battleBench.effects().map(one => `${one.motif}:${one.phase}`));
const film = (page, call, value) => page.evaluate(([name, argument]) => window.battleBench.film[name](argument), [call, value]);

test.beforeEach(async ({ page }) => { await openBench(page, '/table/battle-lab.html'); });

test('the spell art stages spells, and the page reads each staging', () => {
  expect(staged.length).toBeGreaterThanOrEqual(30);
  for (const { id, beats } of staged) expect(beats, id).not.toBeNull();
});

for (const { id, beats, targets } of staged) {
  test(`${id} plays every beat it holds, inside the field's budget, and leaves nothing`, async ({ page }) => {
    // A filter is drawn slowly by a renderer with no graphics card, and a film is stepped frame by frame.
    test.setTimeout(beats.some(beat => beat.beat === 'pulse') ? 120000 : 60000);
    await film(page, 'start', { spell: id, actor: 'a1', targets, damage: 5 });
    const seen = new Set();
    let most = 0, changed = 0, drawn = false;
    for (let elapsed = 0; elapsed < WINDUP_MS + IMPACT_MS; elapsed += STEP_MS) {
      const playing = await kit(page);
      for (const one of playing.objects) seen.add(one.beat);
      most = Math.max(most, playing.particles);
      changed = Math.max(changed, playing.changes);
      drawn ||= (await motifs(page)).some(one => one.endsWith(':impact'));
      // A staged spell draws no shared charge: its windup is what its beats say.
      if (elapsed < WINDUP_MS) expect((await motifs(page)).filter(one => one.endsWith(':windup') && !one.startsWith('quill'))).toEqual([]);
      await film(page, 'advance', STEP_MS);
    }
    expect([...seen].sort()).toEqual([...new Set(beats.map(beat => beat.beat).filter(beat => OBJECTS.includes(beat)))].sort());
    expect(most).toBeLessThanOrEqual(SCENE_PARTICLES);
    expect(changed > 0).toBe(beats.some(beat => beat.beat === 'pulse' || beat.beat === 'flash'));
    expect(drawn).toBe(true);
    expect(await kit(page)).toEqual({ objects: [], particles: 0, changes: 0 });
    expect(await motifs(page)).toEqual([]);
    await film(page, 'stop');
  });
}

test('a stop at the height of the richest staging leaves nothing of it', async ({ page }) => {
  const richest = staged.toSorted((one, other) => other.beats.length - one.beats.length)[0];
  await film(page, 'start', { spell: richest.id, actor: 'a1', targets: richest.targets, damage: 5 });
  await film(page, 'advance', WINDUP_MS + 120);
  expect((await kit(page)).objects.length).toBeGreaterThan(0);
  // Stopping is the path a skip, a rotation and a hidden tab take.
  await page.evaluate(() => window.battleBench.stop());
  expect(await kit(page)).toEqual({ objects: [], particles: 0, changes: 0 });
  expect(await motifs(page)).toEqual([]);
  await film(page, 'stop');
});

// The acceptance of the whole set (BC10): with less motion every entry of the spell art is its settled frame from
// the first step. On the film's clock, through the time its windup and its contact would take: no beat, no
// motif, no figure off its home, leaning, stretched, breathing or tinted, and a view that does not move, though
// the fixture says each creature struck lost health. With full motion the same steps see the caster load and
// the blow land, so the reading is not one a resolved action passes anyway.
const HOME = { x: 0, y: 0, angle: 0, width: 1, height: 1, tinted: false };
const STILL = { trauma: 0, x: 0, y: 0, zoom: 1 };
const moved = ({ figures, view }) => figures.some(one => Object.entries(HOME).some(([key, value]) => one[key] !== value)) || Object.entries(STILL).some(([key, value]) => view[key] !== value);
const standing = page => page.evaluate(() => window.battleBench.motion());

test('with less motion no entry of the spell art plays a beat or a motif, moves a figure or moves the view', async ({ page }) => {
  test.setTimeout(300000);
  expect(staged.map(one => one.id).sort()).toEqual(Object.keys(art).sort());
  await page.emulateMedia({ reducedMotion: 'reduce' });
  // The bench rests when the media query's change event arrives: the attribute says it has (#894).
  await expect(page.locator('#lab')).toHaveAttribute('data-motion', 'reduced');
  for (const { id, targets } of staged) {
    await film(page, 'start', { spell: id, actor: 'a1', targets, damage: 5 });
    await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'resolved');
    for (let elapsed = 0; elapsed <= WINDUP_MS + 240; elapsed += 180) {
      expect(await kit(page), `${id} at ${elapsed} ms`).toEqual({ objects: [], particles: 0, changes: 0 });
      expect(await motifs(page), `${id} at ${elapsed} ms`).toEqual([]);
      const now = await standing(page);
      for (const one of now.figures) expect(one, `${id} at ${elapsed} ms: ${one.id}`).toMatchObject(HOME);
      expect(now.view, `${id} at ${elapsed} ms`).toEqual(STILL);
      await film(page, 'advance', 180);
    }
    await film(page, 'stop');
  }
});

test('with full motion the same reading sees a figure or the view move, so it says something', async ({ page }) => {
  const [{ id, targets }] = staged;
  await film(page, 'start', { spell: id, actor: 'a1', targets, damage: 5 });
  let seen = false;
  for (let elapsed = 0; elapsed <= WINDUP_MS + 240; elapsed += 180) {
    seen ||= moved(await standing(page));
    await film(page, 'advance', 180);
  }
  expect(seen).toBe(true);
  await film(page, 'stop');
});

// The frame time script (scripts/battle/frametime.mjs) is run by hand, for the record: its numbers are a
// machine's and no test holds them. What is held is that it measures: a time a step over the whole action, a
// time a frame until the action resolves, and a bench left at rest.
test('the frame time script times every step of an action, then every frame of its play', async ({ page }) => {
  const [{ id, targets }] = staged;
  const steps = await measureSteps(page, { spell: id, targets });
  expect(steps).toHaveLength(Math.ceil(benchDuration(1) / FILM_TICK_MS));
  for (const one of steps) expect(one).toBeGreaterThanOrEqual(0);
  await expect(page.locator('#lab')).not.toHaveAttribute('data-film');
  const frames = await measureFrames(page, { spell: id, targets, speed: 2 });
  // At 2× the action lasts half as long: the frames counted are those of the play, and of nothing after it.
  const played = frames.reduce((sum, one) => sum + one, 0);
  expect(played).toBeGreaterThanOrEqual(benchDuration(2) - 100);
  expect(played).toBeLessThan(benchDuration(2) + 1500);
  await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'resolved');
  expect(await kit(page)).toEqual({ objects: [], particles: 0, changes: 0 });
});

// A caster that pays in blood and is among the recipients of its own ward receives the spell as well as the wound
// (#946, #949), so the scene draws the staging's target beats on it too. A trail from the caster to itself would
// have no length, so it draws none.
test('a ward paid for in blood is staged on its caster like every other recipient, but with no trail to itself', async ({ page }) => {
  test.setTimeout(60000);
  const spell = 'spell:revenant_guards:v1';
  await film(page, 'start', { spell, actor: 'a1', targets: ['a1', 'a2', 'a3'], wound: true });
  expect(await page.locator('#lab').getAttribute('data-wound')).toBe('true');
  const beats = new Map();
  for (let elapsed = 0; elapsed < WINDUP_MS + IMPACT_MS; elapsed += STEP_MS) {
    for (const one of (await kit(page)).objects) beats.set(one.creature, new Set([...(beats.get(one.creature) ?? []), one.beat]));
    await film(page, 'advance', STEP_MS);
  }
  const on = id => [...(beats.get(id) ?? [])].sort();
  for (const id of ['a1', 'a2', 'a3']) expect(on(id), `${id} holds the ward's flow`).toContain('flow');
  for (const id of ['a2', 'a3']) expect(on(id), `the trail reaches ${id}`).toContain('trail');
  expect(on('a1'), 'no trail from the caster to itself').not.toContain('trail');
  await film(page, 'stop');
});
