import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { test, expect } from '@playwright/test';
import { IMPACT_MS, WINDUP_MS } from '../../table/battle-timing.js';
import { SCENE_PARTICLES, stagingBeats } from '../../table/battle-staging.js';
import { openBench } from '../../scripts/battle/filmstrip.mjs';

// The stagings the spell art carries (docs/art/battle-craft-plan.md, BC6 to BC8): each staged spell is played on
// the bench, on the film's clock, as the content says it is cast: on as many figures as it may reach. Each beat
// it holds is seen alive, the field never holds more particles than its budget, and nothing is left once the
// action has resolved. One Phaser page at a time, as the bench's spec.
test.describe.configure({ mode: 'default' });

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const art = JSON.parse(readFileSync(join(root, 'data/presentation/spell-art.json'), 'utf8'));
const targeting = Object.fromEntries(readdirSync(join(root, 'data/Spells'), { withFileTypes: true, recursive: true })
  .filter(entry => entry.isFile() && entry.name.endsWith('.json'))
  .map(entry => JSON.parse(readFileSync(join(entry.parentPath, entry.name), 'utf8')))
  .map(spell => [spell.id, spell.targeting ?? {}]));

// Who the bench plays a spell on: the caster itself, one ally, or as many enemies as the spell may reach.
function targetsOf(id) {
  const { origin, maxTargets = 1 } = targeting[id] ?? {};
  if (origin === 'Self') return ['a1'];
  if (origin === 'Ally') return ['a2'];
  return ['b1', 'b2', 'b3'].slice(0, maxTargets);
}

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
  expect(staged.length).toBeGreaterThanOrEqual(16);
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

test('with less motion no staged spell plays a beat or a motif', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  // The bench rests when the media query's change event arrives: the attribute says it has (#894).
  await expect(page.locator('#lab')).toHaveAttribute('data-motion', 'reduced');
  for (const { id, targets } of staged) {
    await page.evaluate(([spell, on]) => { window.battleBench.choose({ spell, actor: 'a1', targets: on }); window.battleBench.play(); }, [id, targets]);
    await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'resolved');
    expect(await kit(page), id).toEqual({ objects: [], particles: 0, changes: 0 });
    expect(await motifs(page), id).toEqual([]);
  }
});
