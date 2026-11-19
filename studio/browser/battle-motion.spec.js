import { test, expect } from '@playwright/test';
import { IMPACT_MS, WINDUP_MS } from '../../table/battle-timing.js';
import { BREATH, DEFEATED_ALPHA, FLIGHT, PUNCH, TRAUMA, WEIGHTS, flightAt, meleeBeats } from '../../table/battle-motion.js';
import { openBench } from '../../scripts/battle/filmstrip.mjs';

// The movement every action shares (docs/art/storyboards/movement.md, BC5 of docs/art/battle-craft-plan.md),
// played on the bench on the film's clock. One Phaser page at a time, as the bench's spec.
test.describe.configure({ mode: 'default' });

const film = (page, call, value) => page.evaluate(([name, argument]) => window.battleBench.film[name](argument), [call, value]);
const motion = page => page.evaluate(() => window.battleBench.motion());
const figure = async (page, id) => (await motion(page)).figures.find(one => one.id === id);
const HOME = { x: 0, y: 0, angle: 0, width: 1, tinted: false };
const far = one => Math.hypot(one.x, one.y);
const pummel = { spell: 'spell:pummel:v1', actor: 'a1', targets: ['b1'] };

test.beforeEach(async ({ page }) => { await openBench(page, '/table/battle-lab.html'); });

test('a standing figure breathes, each out of step with its neighbour, and a figure an action moves does not', async ({ page }) => {
  await film(page, 'start', pummel);
  const heights = { b2: [], b3: [], a1: [] };
  for (let step = 0; step < BREATH.period / 200; step++) {
    const now = await motion(page);
    for (const id of Object.keys(heights)) heights[id].push(now.figures.find(one => one.id === id).height);
    await film(page, 'advance', 200);
  }
  for (const id of ['b2', 'b3']) {
    const swing = Math.max(...heights[id]) - Math.min(...heights[id]);
    expect(swing, `${id} breathes by about 1.5% of its height`).toBeGreaterThan(BREATH.amount * 1.8);
    expect(swing).toBeLessThanOrEqual(BREATH.amount * 2 + .001);
  }
  expect(heights.b2.indexOf(Math.max(...heights.b2))).not.toBe(heights.b3.indexOf(Math.max(...heights.b3)));
  // The caster's tweens hold it from the first frame: through its draw back its height does not move, while its
  // neighbours' does.
  expect(new Set(heights.a1.slice(0, 4)).size).toBe(1);
  expect(new Set(heights.b2.slice(0, 4)).size).toBeGreaterThan(1);
  // The breath moves the height alone, about the feet: no figure it moves leaves its home.
  expect(await figure(page, 'b2')).toMatchObject(HOME);
  await film(page, 'stop');
});

test('with less motion no figure breathes, and a blow moves none', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await film(page, 'start', { ...pummel, damage: 8 });
  for (let step = 0; step < 8; step++) {
    const now = await motion(page);
    for (const one of now.figures) expect(one, `${one.id} at step ${step}`).toMatchObject({ ...HOME, height: 1 });
    expect(now.view).toEqual({ trauma: 0, x: 0, y: 0, zoom: 1 });
    await film(page, 'advance', 400);
  }
  await film(page, 'stop');
});

// The speeds the bench offers. 1.5×, which a seat also offers, is held by the unit test that every duration the
// body plays is divided by the rate (`table/battle-motion.test.js`).
for (const speed of [0.5, 1, 2]) {
  test(`at ${speed}× the blow lands when the windup ends: the draw back, the contact, the flash, the recoil, then home`, async ({ page }) => {
    // The whole action is stepped frame by frame, 5 s of it at 0.5×: a loaded runner needs the room (#784).
    test.setTimeout(60000);
    const at = ms => Math.round(ms / speed / 20) * 20;
    const beats = meleeBeats('heavy');
    await film(page, 'start', { ...pummel, damage: 8, speed });
    await film(page, 'advance', at(beats.draw.lasts + 20));
    const drawn = await figure(page, 'a1');
    expect(far(drawn), 'the caster has drawn back').toBeGreaterThan(WEIGHTS.heavy.draw - 3);
    // Until the contact the target stands, untouched.
    await film(page, 'advance', at(WINDUP_MS) - at(beats.draw.lasts + 20) - 20);
    expect(await figure(page, 'b1')).toMatchObject(HOME);
    await film(page, 'advance', 40);
    const struck = await figure(page, 'b1');
    expect(struck.tinted, 'the target flashes on the contact').toBe(true);
    expect((await motion(page)).view.trauma).toBeGreaterThan(0);
    // The contact pose is whole, whatever the frame the impact arrived on: at its target, leaning, fully stretched.
    const landed = await figure(page, 'a1');
    expect(far(landed), 'the caster reached its target').toBeGreaterThan(40);
    expect(Math.abs(landed.angle)).toBe(beats.strike.lean);
    expect(Math.max(landed.width, landed.height)).toBeCloseTo(1 + beats.stretch.amount, 2);
    // After the stop the target is thrown back, further than a light blow would send it.
    await film(page, 'advance', at(WEIGHTS.heavy.stop + 280));
    const thrown = await figure(page, 'b1');
    expect(thrown.tinted).toBe(false);
    expect(far(thrown)).toBeGreaterThan(WEIGHTS.medium.recoil);
    // Before the action ends both are home, and the view is still.
    await film(page, 'advance', at(IMPACT_MS) - at(WEIGHTS.heavy.stop + 280) - 60);
    const now = await motion(page);
    for (const id of ['a1', 'b1']) expect(now.figures.find(one => one.id === id), id).toMatchObject(HOME);
    expect(now.view).toEqual({ trauma: 0, x: 0, y: 0, zoom: 1 });
    await film(page, 'stop');
  });
}

test('a blow that takes more health is drawn back further and throws its target further', async ({ page }) => {
  const reach = async damage => {
    await film(page, 'start', { ...pummel, damage });
    await film(page, 'advance', 260);
    const drawn = far(await figure(page, 'a1'));
    await film(page, 'advance', WINDUP_MS - 260 + meleeBeats(damage >= 7 ? 'heavy' : 'light').stop + 300);
    const thrown = far(await figure(page, 'b1'));
    await film(page, 'stop');
    return { drawn, thrown };
  };
  const light = await reach(2), heavy = await reach(8);
  expect(heavy.drawn).toBeGreaterThan(light.drawn + 6);
  expect(heavy.thrown).toBeGreaterThan(light.thrown + 6);
});

test('a blow that takes no health throws its target and flashes nothing, and the view stays still', async ({ page }) => {
  await film(page, 'start', { ...pummel, damage: 0 });
  await film(page, 'advance', WINDUP_MS + 20);
  expect((await figure(page, 'b1')).tinted).toBe(false);
  expect((await motion(page)).view.trauma).toBe(0);
  await film(page, 'advance', 340);
  expect(far(await figure(page, 'b1'))).toBeGreaterThan(8);
  await film(page, 'stop');
});

test('a caster who does not lunge loads where it stands, and a figure harmed from afar is thrown back', async ({ page }) => {
  // A projectile: the caster never leaves its ground by more than its load, and the target answers its loss.
  await film(page, 'start', { spell: 'spell:throwing_star:v1', actor: 'a1', targets: ['b1'], damage: 5 });
  let furthest = 0;
  for (let step = 0; step < WINDUP_MS / 60; step++) {
    await film(page, 'advance', 60);
    furthest = Math.max(furthest, far(await figure(page, 'a1')));
  }
  expect(furthest).toBeGreaterThan(4);
  expect(furthest).toBeLessThanOrEqual(8.5);
  await film(page, 'advance', 20);
  expect((await figure(page, 'b1')).tinted).toBe(true);
  await film(page, 'advance', 340);
  expect(far(await figure(page, 'b1'))).toBeGreaterThan(WEIGHTS.light.recoil);
  await film(page, 'stop');
  // A cast on an ally: the caster squashes, then stands tall on the contact, and the ally does not move.
  await film(page, 'start', { spell: 'spell:adrenaline_tonic:v1', actor: 'a1', targets: ['a2'] });
  await film(page, 'advance', 600);
  expect((await figure(page, 'a1')).height).toBeLessThan(.97);
  await film(page, 'advance', 240);
  const tall = await figure(page, 'a1');
  expect(tall.height).toBeGreaterThan(1.03);
  expect(far(tall)).toBe(0);
  expect(await figure(page, 'a2')).toMatchObject({ x: 0, y: 0, angle: 0, tinted: false });
  expect((await motion(page)).view.trauma).toBe(0);
  await film(page, 'stop');
});

test('a defeated creature is seen to fall, and ends on the settled frame', async ({ page }) => {
  await film(page, 'start', { ...pummel, damage: 8, defeat: true });
  await film(page, 'advance', WINDUP_MS + 20);
  const hit = await motion(page);
  // At the contact it still stands in full, flashed; the view trembles at its ceiling and the zoom punches in.
  expect(hit.figures.find(one => one.id === 'b1')).toMatchObject({ alpha: 1, tinted: true });
  expect(hit.view.trauma).toBeGreaterThan(TRAUMA.ceiling - .1);
  expect(hit.view.zoom).toBeGreaterThan(1 + PUNCH.amount * .6);
  await film(page, 'advance', 500);
  const thrown = await figure(page, 'b1');
  expect(far(thrown)).toBeGreaterThan(20);
  expect(thrown.alpha).toBe(1);
  expect((await motion(page)).view).toEqual({ trauma: 0, x: 0, y: 0, zoom: 1 });
  await film(page, 'advance', 700);
  const sinking = await figure(page, 'b1');
  expect(sinking.alpha).toBeLessThan(.9);
  expect(sinking.alpha).toBeGreaterThan(DEFEATED_ALPHA);
  expect(sinking.height).toBeLessThan(.97);
  await film(page, 'advance', 400);
  // The settled frame, before the action ends: home, upright, its own scale, untinted, faded, and no breath.
  const settled = { ...HOME, height: 1, alpha: DEFEATED_ALPHA, shadow: DEFEATED_ALPHA };
  expect(await figure(page, 'b1')).toMatchObject(settled);
  await film(page, 'advance', 1200);
  expect(await figure(page, 'b1')).toMatchObject(settled);
  await film(page, 'stop');
});

for (const [name, ms] of [['the flash and the stop', WINDUP_MS + 40], ['the fall', WINDUP_MS + 900], ['the strike', WINDUP_MS - 60]]) {
  test(`a stop during ${name} of a death leaves every figure home, at its scale, untinted, and the view still`, async ({ page }) => {
    await film(page, 'start', { ...pummel, damage: 8, defeat: true });
    await film(page, 'advance', ms);
    // Stopping is the path a skip, a rotation and a hidden tab take.
    await page.evaluate(() => window.battleBench.stop());
    const now = await motion(page);
    for (const one of now.figures) expect(one, one.id).toMatchObject({ ...HOME, alpha: 1 });
    expect(now.view).toEqual({ trauma: 0, x: 0, y: 0, zoom: 1 });
    await film(page, 'stop');
  });
}

test('a blood cost alone does not move the view, and the caster does not answer its own cost', async ({ page }) => {
  await film(page, 'start', { spell: 'spell:bone_ward:v1', actor: 'a1', targets: ['a1'], wound: true });
  for (let step = 0; step < 12; step++) {
    await film(page, 'advance', 120);
    const now = await motion(page);
    expect(now.view).toEqual({ trauma: 0, x: 0, y: 0, zoom: 1 });
    expect(now.figures.find(one => one.id === 'a1')).toMatchObject({ x: 0, y: 0, tinted: false });
  }
  await film(page, 'stop');
});

// #909: the thrower let go at 240 ms and its quill, eased in, stood on its body until about 540 ms.
test('a quill is not drawn before the throw, leaves the thrower as it lets go, and lands on the contact', async ({ page }) => {
  const quill = async () => (await page.evaluate(() => window.battleBench.effects())).find(one => one.motif === 'quill');
  const anchors = await page.evaluate(() => window.battleBench.anchors());
  const [a1, b3] = ['a1', 'b3'].map(id => anchors.find(one => one.id === id));
  const start = flightAt(a1, b3, 0), whole = Math.hypot(b3.x - start.x, b3.y - start.y);
  const flown = one => Math.hypot(one.x - start.x, one.y - start.y);
  await film(page, 'start', { spell: 'spell:throwing_star:v1', actor: 'a1', targets: ['b3'], damage: 3 });
  // While the body loads, nothing stands on it.
  for (let elapsed = 0; elapsed < FLIGHT.at; elapsed += 60) {
    expect((await quill()).visible, `at ${elapsed} ms`).toBe(false);
    await film(page, 'advance', 60);
  }
  // 60 ms after the throw it is seen, and already more than its own length away: out of the thrower's hands.
  await film(page, 'advance', 60);
  const early = await quill();
  expect(early.visible).toBe(true);
  expect(flown(early)).toBeGreaterThan(whole * .06);
  // Half way through the flight it is about half way along its arc, a tick or two of the film's clock behind at
  // most, and above the straight line: a low arc.
  await film(page, 'advance', FLIGHT.lasts / 2 - 60);
  const middle = await quill();
  const shares = Array.from({ length: 11 }, (_, index) => .4 + index * .01);
  const off = Math.min(...shares.map(share => { const on = flightAt(a1, b3, share); return Math.hypot(middle.x - on.x, middle.y - on.y); }));
  expect(off).toBeLessThan(3);
  expect(middle.y).toBeLessThan((start.y + b3.y) / 2 - 4);
  // One frame before the contact it is on its target, and the thrower is home.
  await film(page, 'advance', FLIGHT.lasts / 2 - 20);
  expect(flown(await quill())).toBeGreaterThan(whole * .9);
  expect(far(await figure(page, 'a1'))).toBeLessThan(1);
  await film(page, 'stop');
});
