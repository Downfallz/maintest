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

// The acceptance of the whole set (BC10): with less motion every entry of the spell art goes straight to its
// settled frame. No beat, no motif, no figure off its home, leaning, stretched or tinted, and a view that does
// not move, though the fixture says each creature struck lost health.
test('with less motion no entry of the spell art plays a beat or a motif, moves a figure or moves the view', async ({ page }) => {
  expect(staged.map(one => one.id).sort()).toEqual(Object.keys(art).sort());
  await page.emulateMedia({ reducedMotion: 'reduce' });
  // The bench rests when the media query's change event arrives: the attribute says it has (#894).
  await expect(page.locator('#lab')).toHaveAttribute('data-motion', 'reduced');
  for (const { id, targets } of staged) {
    await page.evaluate(([spell, on]) => { window.battleBench.choose({ spell, actor: 'a1', targets: on, damage: 5 }); window.battleBench.play(); }, [id, targets]);
    await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'resolved');
    expect(await kit(page), id).toEqual({ objects: [], particles: 0, changes: 0 });
    expect(await motifs(page), id).toEqual([]);
    const { figures, view } = await page.evaluate(() => window.battleBench.motion());
    for (const one of figures) expect(one, `${id}: ${one.id}`).toMatchObject({ x: 0, y: 0, angle: 0, width: 1, height: 1, tinted: false });
    expect(view, id).toEqual({ trauma: 0, x: 0, y: 0, zoom: 1 });
  }
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
