import { test, expect } from '@playwright/test';
import { WINDUP_MS } from '../../table/battle-timing.js';
import { DEPTH, EMBER_COUNT, FOG, LIGHT, LIGHT_COLOURS, REST_TINT } from '../../table/battle-ground.js';
import { openBench } from '../../scripts/battle/filmstrip.mjs';

// The ground (docs/art/storyboards/ground.md, BC9 of docs/art/battle-craft-plan.md): the air at rest, the depth of
// its far layers when the view moves, and the eclipse's light answering a blow, played on the bench on the film's
// clock. One Phaser page at a time, as the bench's spec.
test.describe.configure({ mode: 'default' });

const film = (page, call, value) => page.evaluate(([name, argument]) => window.battleBench.film[name](argument), [call, value]);
const ground = async page => (await page.evaluate(() => window.battleBench.motion())).ground;
const view = async page => (await page.evaluate(() => window.battleBench.motion())).view;
const hex = colour => `0x${colour.toString(16).padStart(6, '0')}`;
const REST = hex(REST_TINT.painting);
const DARK = { strength: 0, colour: null, tint: REST, shown: false };
const STILL = { x: 0, y: 0, scale: 1 };
const defeat = { spell: 'spell:pummel:v1', actor: 'a1', targets: ['b1'], damage: 8, defeat: true };
// The round's scenery is waited for before a film, as the film strip script does.
async function standIn(page, round) {
  await page.evaluate(asked => window.battleBench.choose({ round: asked }), round);
  await page.waitForFunction(() => !window.battleBench.loading());
}

test.beforeEach(async ({ page }) => { await openBench(page, '/table/battle-lab.html'); });

test('at rest the fog drifts and the embers rise on the ambient clock, and nothing else of the ground moves', async ({ page }) => {
  await film(page, 'start', { still: true, actor: 'a1' });
  const first = await ground(page);
  expect(first.embers).toEqual({ shown: EMBER_COUNT, alpha: .6 });
  expect(first.light).toEqual(DARK);
  await film(page, 'advance', 2000);
  const later = await ground(page);
  expect(later.fog.drift - first.fog.drift).toBeCloseTo(2000 * FOG.drift, 2);
  for (const [name, layer] of Object.entries(later.layers)) expect(layer, name).toEqual(STILL);
  // Round 1: the sky is the painting's own, and has no depth of its own.
  expect(later.layers.sky).toBeUndefined();
  expect(later.light).toEqual(DARK);
  // The embers are behind every figure, and the gold motes are gone.
  const drawn = await page.evaluate(() => window.battleBench.groundObjects());
  expect(drawn.embers).toBe(EMBER_COUNT);
  expect(drawn.deepest).toBeLessThan(0);
  expect(drawn.fog).toBe(true);
  await film(page, 'stop');
});

test('the embers are brighter once the floor has broken', async ({ page }) => {
  await standIn(page, 12);
  await film(page, 'start', { still: true, actor: 'a1', round: 12 });
  expect((await ground(page)).embers).toEqual({ shown: EMBER_COUNT, alpha: 1 });
  expect((await ground(page)).layers.sky).toEqual(STILL);
  await film(page, 'stop');
});

test('with less motion nothing of the ground moves: no ember, no drift, no depth, no light, and the fog stays', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await standIn(page, 12);
  await film(page, 'start', { ...defeat, round: 12 });
  for (let step = 0; step < 8; step++) {
    const now = await ground(page);
    // The embers drawn, read from the scene: not a mock route's `shown`, which a spec polls.
    const embers = now.embers.shown;
    expect(embers, `step ${step}`).toBe(0);
    expect(now.fog).toMatchObject({ drift: 0, visible: true });
    expect(now.light).toEqual(DARK);
    for (const [name, layer] of Object.entries(now.layers)) expect(layer, name).toEqual(STILL);
    await film(page, 'advance', 400);
  }
  await film(page, 'stop');
});

test('a blow that defeats lights the eclipse once in its family\'s colour, and the ground is at rest again 320 ms after the contact', async ({ page }) => {
  await standIn(page, 12);
  await film(page, 'start', { ...defeat, round: 12 });
  await film(page, 'advance', WINDUP_MS - 40);
  expect((await ground(page)).light).toEqual(DARK);
  const seen = [];
  for (let ms = WINDUP_MS; ms <= WINDUP_MS + LIGHT.rise + LIGHT.fall + 80; ms += 20) {
    await film(page, 'advance', ms === WINDUP_MS ? 40 : 20);
    seen.push({ ms, ...(await ground(page)).light });
  }
  const peak = seen.reduce((best, one) => one.strength > best.strength ? one : best);
  expect(peak).toMatchObject({ strength: 1, colour: hex(LIGHT_COLOURS.physical), shown: true });
  expect(peak.tint).not.toBe(REST);
  expect(peak.ms).toBeLessThanOrEqual(WINDUP_MS + LIGHT.rise + 40);
  // One rise and one fall: once it has started to fall it never rises again.
  const after = seen.slice(seen.indexOf(peak));
  for (let index = 1; index < after.length; index++) expect(after[index].strength).toBeLessThanOrEqual(after[index - 1].strength);
  for (const one of seen.filter(one => one.ms >= WINDUP_MS + LIGHT.rise + LIGHT.fall + 40)) expect(one, `at ${one.ms} ms`).toMatchObject(DARK);
  await film(page, 'stop');
});

test('while the view moves the sky lies half as far behind it as the floor, and comes back with it', async ({ page }) => {
  await standIn(page, 12);
  await film(page, 'start', { ...defeat, round: 12 });
  await film(page, 'advance', WINDUP_MS);
  let moved = 0, zoomed = 0;
  for (let step = 0; step < 14; step++) {
    await film(page, 'advance', 20);
    const [{ layers }, offset] = [await ground(page), await view(page)];
    if (offset.x !== 0 || offset.y !== 0) moved++;
    if (offset.zoom > 1) zoomed++;
    expect(layers.sky.x).toBeCloseTo(offset.x * (1 - DEPTH.sky), 1);
    expect(layers.sky.y).toBeCloseTo(offset.y * (1 - DEPTH.sky), 1);
    expect(layers.fog.x).toBeCloseTo(offset.x * (1 - DEPTH.fog), 1);
    expect(layers.far.y).toBeCloseTo(offset.y * (1 - DEPTH.far), 1);
    // On the screen the sky zooms by half of the punch: its own scale takes the other half back.
    expect(layers.sky.scale * offset.zoom).toBeCloseTo(1 + DEPTH.sky * (offset.zoom - 1), 2);
    for (const name of ['painting', 'floor', 'arch', 'near']) expect(layers[name], name).toEqual(STILL);
  }
  expect(moved).toBeGreaterThan(3);
  expect(zoomed).toBeGreaterThan(1);
  await film(page, 'advance', 600);
  for (const [name, layer] of Object.entries((await ground(page)).layers)) expect(layer, name).toEqual(STILL);
  await film(page, 'stop');
});

for (const [name, play] of [
  ['a base action that takes 3 health', { spell: 'spell:heavy_strike:v1', actor: 'a1', targets: ['b1'], damage: 3 }],
  ['a fizzle', { spell: 'spell:pummel:v1', actor: 'a1', targets: ['b1'], outcome: 'fizzled' }],
  ['a heal', { spell: 'spell:rejuvenate:v1', actor: 'a1', targets: ['a2'] }],
  ['a blood cost alone', { spell: 'spell:bone_ward:v1', actor: 'a1', targets: ['a1'], wound: true }],
]) {
  test(`${name} leaves the ground's light as it rests`, async ({ page }) => {
    await film(page, 'start', play);
    for (let ms = 0; ms <= WINDUP_MS + 600; ms += 60) {
      expect((await ground(page)).light, `at ${ms} ms`).toEqual(DARK);
      await film(page, 'advance', 60);
    }
    await film(page, 'stop');
  });
}

test('a stop while the eclipse is lit puts the ground back: no light, every tint at its rest, every layer where it lies', async ({ page }) => {
  await standIn(page, 12);
  await film(page, 'start', { ...defeat, round: 12 });
  await film(page, 'advance', WINDUP_MS + LIGHT.rise);
  expect((await ground(page)).light.strength).toBeGreaterThan(.5);
  // Stopping is the path a skip, a rotation, a seat change and a hidden tab take.
  await page.evaluate(() => window.battleBench.stop());
  await film(page, 'advance', 20);
  const now = await ground(page);
  expect(now.light).toEqual(DARK);
  for (const [name, layer] of Object.entries(now.layers)) expect(layer, name).toEqual(STILL);
  expect((await page.evaluate(() => window.battleBench.groundObjects())).tints).toEqual([REST]);
  await film(page, 'stop');
});

test('a hidden tab stops the air, and it goes on from where it stood when the tab is seen again', async ({ page }) => {
  const drift = async () => (await ground(page)).fog.drift;
  const before = await drift();
  await expect.poll(drift).toBeGreaterThan(before);
  const hide = hidden => page.evaluate(now => {
    Object.defineProperty(document, 'hidden', { configurable: true, get: () => now });
    Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => now ? 'hidden' : 'visible' });
    document.dispatchEvent(new Event('visibilitychange'));
  }, hidden);
  await hide(true);
  const stood = await drift();
  await page.waitForTimeout(1200);
  expect(await drift()).toBe(stood);
  await hide(false);
  await expect.poll(drift).toBeGreaterThan(stood);
  // No jump: the 1.2 s the tab was hidden are not caught up (3.6 px of drift); it resumes within a step or two.
  expect(await drift() - stood).toBeLessThan(1200 * FOG.drift);
});
