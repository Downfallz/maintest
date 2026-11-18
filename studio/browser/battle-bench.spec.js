import { readFileSync } from 'node:fs';
import { test, expect } from '@playwright/test';
import { IMPACT_MS, WINDUP_MS } from '../../table/battle-timing.js';
import { filmFrames, filmSheet, openBench } from '../../scripts/battle/filmstrip.mjs';
import { fitsViewport } from './fit.js';

// The effects bench (docs/art/battle-craft-plan.md, BC2): one Phaser page at a time, as the laboratory's spec.
test.describe.configure({ mode: 'default' });

const art = JSON.parse(readFileSync(new URL('../../data/presentation/spell-art.json', import.meta.url), 'utf8'));
const effects = page => page.evaluate(() => window.battleBench.effects());
const film = (page, call, value) => page.evaluate(([name, argument]) => window.battleBench.film[name](argument), [call, value]);

test.beforeEach(async ({ page }) => { await openBench(page, '/table/battle-lab.html'); });

test('the bench lists every authored effect and the six creatures, and fits the viewport', async ({ page }) => {
  const listed = await page.locator('#bench-spell option').evaluateAll(options => options.map(option => option.value));
  expect(listed).toHaveLength(44);
  expect([...listed].sort()).toEqual(Object.keys(art).sort());
  await expect(page.locator('#bench-actor option')).toHaveCount(6);
  await expect(page.locator('#bench-targets input')).toHaveCount(6);
  await expect(page.locator('#bench-targets input:checked')).toHaveCount(1);
  await expect(page.locator('#phaser-mount canvas')).toHaveCount(1);
  await expect(page.locator('#lab')).toHaveAttribute('data-asset-fallback', 'false');
  // The sample action's controls are the laboratory's, not the bench's.
  for (const id of ['#choose', '#confirm', '#timeline', '#fullscreen']) await expect(page.locator(id)).toBeHidden();
  await fitsViewport(page, '#bench-spell, #bench-actor, #bench-targets label, #bench-play, #reset, #settings');
});

test('a chosen effect plays on the chosen creatures: its windup, its impact, then nothing left', async ({ page }) => {
  test.slow();
  await page.locator('#bench-spell').selectOption('spell:engulfing_flames:v1');
  await page.locator('#bench-targets input[value="b2"]').check();
  const started = await film(page, 'start');
  expect(started.stage).toBe('windup');
  expect(started.duration).toBe(WINDUP_MS + IMPACT_MS);
  expect((await effects(page)).map(one => one.phase)).toEqual(['windup']);
  expect((await film(page, 'advance', WINDUP_MS)).stage).toBe('impact');
  await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'impact');
  expect((await effects(page)).map(one => [one.motif, one.creature, one.phase])).toEqual([['fire', 'b1', 'impact'], ['fire', 'b2', 'impact']]);
  expect(await film(page, 'advance', IMPACT_MS)).toEqual({ elapsed: WINDUP_MS + IMPACT_MS, stage: 'resolved' });
  expect(await effects(page)).toEqual([]);
  await film(page, 'stop');
  await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'decision');
  // The loop runs again once the film ends: the button plays the effect on the page's own clock.
  await page.locator('#bench-play').click();
  await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'resolved', { timeout: 15000 });
  await expect(page.locator('#bench-status')).toHaveText('Engulfing Flames: played');
});

test('the address can name the effect the bench opens on', async ({ page }) => {
  await openBench(page, '/table/battle-lab.html?bench=spell:frostbite:v1');
  await expect(page.locator('#bench-spell')).toHaveValue('spell:frostbite:v1');
  await expect(page.locator('#lab')).toHaveAttribute('data-spell', 'spell:frostbite:v1');
});

test('an address naming an effect the art does not list opens the bench on the first one, and says so', async ({ page }) => {
  await openBench(page, '/table/battle-lab.html?bench=spell:nothing:v1');
  await expect(page.locator('#bench-spell')).toHaveValue('spell:adrenaline_tonic:v1');
  await expect(page.locator('#bench-status')).toHaveText('No effect named spell:nothing:v1');
  await expect(page.locator('#bench-play')).toBeEnabled();
});

test('a film refuses a speed the page does not offer, and leaves the bench running', async ({ page }) => {
  const refused = await page.evaluate(() => { try { window.battleBench.film.start({ speed: 1.5 }); return ''; } catch (error) { return error.message; } });
  expect(refused).toBe('The bench has no speed 1.5.');
  await expect(page.locator('#playback-speed')).toHaveValue('1');
  await page.locator('#bench-play').click();
  await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'resolved', { timeout: 15000 });
});

test('less motion shows the settled frame and no effect', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.locator('#bench-play').click();
  await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'resolved');
  await expect(page.locator('#lab')).toHaveAttribute('data-motion', 'reduced');
  expect(await effects(page)).toEqual([]);
  await expect(page.locator('#bench-status')).toContainText('less motion');
});

test('two films of one effect give the same sheet, and another effect another', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'phone', 'A sheet is filmed at phone size.');
  test.slow();
  // A physical melee shakes the camera, which draws from Math.random: the sheet repeats only if it is pinned.
  const options = { spell: 'spell:pummel:v1', targets: ['b1', 'b3'] };
  const first = await filmSheet(page, options);
  const second = await filmSheet(page, options);
  expect(first.equals(second)).toBe(true);
  const frames = await filmFrames(page, options);
  expect(frames.map(frame => frame.ms)).toEqual(Array.from({ length: 12 }, (_, index) => index * 240));
  expect(frames[0].png.equals(frames[4].png)).toBe(false);
  const other = await filmSheet(page, { spell: 'spell:frostbite:v1', targets: ['b1', 'b3'] });
  expect(first.equals(other)).toBe(false);
  await testInfo.attach('pummel.png', { body: first, contentType: 'image/png' });
});
