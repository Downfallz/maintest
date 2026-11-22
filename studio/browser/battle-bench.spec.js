import { readFileSync } from 'node:fs';
import { test, expect } from '@playwright/test';
import { IMPACT_MS, WINDUP_MS } from '../../table/battle-timing.js';
import { filmFrames, filmRecord, filmSheet, openBench } from '../../scripts/battle/filmstrip.mjs';
import { fitsViewport } from './fit.js';

// The effects bench (docs/art/battle-craft-plan.md, BC2): one Phaser page at a time, as the laboratory's spec.
test.describe.configure({ mode: 'default' });

const art = JSON.parse(readFileSync(new URL('../../data/presentation/spell-art.json', import.meta.url), 'utf8'));
const effects = page => page.evaluate(() => window.battleBench.effects());
const film = (page, call, value) => page.evaluate(([name, argument]) => window.battleBench.film[name](argument), [call, value]);

test.beforeEach(async ({ page }) => { await openBench(page, '/table/battle-lab.html'); });

test('the bench lists every authored effect and the six creatures, and fits the viewport', async ({ page }) => {
  const listed = await page.locator('#bench-spell optgroup[label="Spells"] option').evaluateAll(options => options.map(option => option.value));
  expect(listed).toHaveLength(44);
  expect([...listed].sort()).toEqual(Object.keys(art).sort());
  await expect(page.locator('#bench-actor option')).toHaveCount(6);
  await expect(page.locator('#bench-targets input')).toHaveCount(6);
  await expect(page.locator('#bench-targets input:checked')).toHaveCount(1);
  await expect(page.locator('#phaser-mount canvas')).toHaveCount(1);
  await expect(page.locator('#lab')).toHaveAttribute('data-asset-fallback', 'false');
  // The sample action's controls are the laboratory's, not the bench's.
  for (const id of ['#choose', '#confirm', '#timeline', '#fullscreen']) await expect(page.locator(id)).toBeHidden();
  await fitsViewport(page, '#bench-spell, #bench-actor, #bench-targets label, #bench-outcome, #bench-damage, .bench-wound, #bench-play, #reset, #settings');
});

test('a chosen effect plays on the chosen creatures: its windup, its impact, then nothing left', async ({ page }) => {
  test.slow();
  // The sample with no staging: what a chosen effect draws, with no beat of the kit beside it.
  await page.locator('#bench-spell').selectOption('kit:plain');
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
  // The field keeps its size from rest to play: a bench that grew a line while playing would resize the scene,
  // and a resize ends the play.
  const field = () => page.locator('#battle-zone').evaluate(zone => [zone.clientWidth, zone.clientHeight]);
  const atRest = await field();
  await page.locator('#bench-play').click();
  await expect(page.locator('#lab')).not.toHaveAttribute('data-stage', 'decision');
  expect(await field()).toEqual(atRest);
  await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'resolved', { timeout: 15000 });
  expect(await field()).toEqual(atRest);
  await expect(page.locator('#bench-status')).toHaveText('Kit: no staging: played');
  // The bench names a spell as its file does: `spell:engulfing_flames:v1` is Incinerate.
  await expect(page.locator('#bench-spell option[value="spell:engulfing_flames:v1"]')).toContainText('Incinerate');
});

// A machine whose fonts run wider squeezed the button onto two lines at rest and one while playing: the field
// resized under the effect, and a resize ends the play. Wider text stands in for those fonts here.
test('an effect plays to its end whatever room the words of the bench take', async ({ page }) => {
  await page.addStyleTag({ content: '#bench { font-size: 15px; }' });
  const height = () => page.locator('#battle-zone').evaluate(zone => zone.clientHeight);
  const atRest = await height();
  await page.locator('#bench-play').click();
  await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'impact', { timeout: 15000 });
  expect(await height()).toBe(atRest);
  await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'resolved', { timeout: 15000 });
  expect(await height()).toBe(atRest);
});

// #867: each spell is listed as its file names it, with the mutation that casts it.
test('the bench lists each spell with the mutation that casts it, and a film is titled the same', async ({ page }) => {
  await expect(page.locator('#bench-spell option[value="spell:pummel:v1"]')).toHaveText('Pummel · Brute, Stage I · physical · impact · melee');
  await expect(page.locator('#bench-spell option[value="spell:heavy_strike:v1"]')).toHaveText('Strike · starting spell · physical · impact · melee');
  const frames = await filmFrames(page, { spell: 'spell:pummel:v1', frames: 1 });
  expect(frames).toHaveLength(1);
  expect(await page.evaluate(() => window.battleBench.title())).toBe('Pummel · Brute, Stage I · physical · impact · melee · 1×');
  await expect(page.locator('#lab')).toHaveAttribute('data-outcome', 'played');
});

// #843: a fizzle and a blood cost, each a choice of the fixture, drawn as the live scene draws them.
test('a fizzle plays the caster\'s fizzle mark and nothing on the targets', async ({ page }) => {
  await page.locator('#bench-spell').selectOption('spell:pummel:v1');
  await page.locator('#bench-outcome').selectOption('fizzled');
  await expect(page.locator('#bench-wound')).toBeDisabled();
  await film(page, 'start');
  await film(page, 'advance', WINDUP_MS + 40);
  expect((await effects(page)).map(one => [one.motif, one.creature])).toEqual([['fizzle', 'a1']]);
  await film(page, 'stop');
  await expect(page.locator('#lab')).toHaveAttribute('data-outcome', 'fizzled');
});

test('a caster who pays in blood takes a wound while its spell lands on the target', async ({ page }) => {
  await film(page, 'start', { spell: 'spell:pummel:v1', actor: 'a1', targets: ['b1'], wound: true });
  await expect(page.locator('#bench-wound')).toBeChecked();
  await film(page, 'advance', WINDUP_MS + 40);
  expect((await effects(page)).map(one => [one.motif, one.creature]).sort()).toEqual([['impact', 'b1'], ['wound', 'a1']]);
  await film(page, 'stop');
  // The film strip asks for both each time, so a sheet never inherits the last one's choices.
  await film(page, 'start', { spell: 'spell:pummel:v1', actor: 'a1', targets: ['b1'], outcome: 'played', wound: false });
  await film(page, 'advance', WINDUP_MS + 40);
  expect((await effects(page)).map(one => one.motif)).toEqual(['impact']);
  await film(page, 'stop');
});

test('the address can name the effect the bench opens on', async ({ page }) => {
  await openBench(page, '/table/battle-lab.html?bench=spell:frostbite:v1');
  await expect(page.locator('#bench-spell')).toHaveValue('spell:frostbite:v1');
  await expect(page.locator('#lab')).toHaveAttribute('data-spell', 'spell:frostbite:v1');
});

test('an address naming an effect the art does not list opens the bench on the first one, and says so', async ({ page }) => {
  await openBench(page, '/table/battle-lab.html?bench=spell:nothing:v1');
  const first = await page.locator('#bench-spell optgroup[label="Spells"] option').first().getAttribute('value');
  await expect(page.locator('#bench-spell')).toHaveValue(first);
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

// A film steps the scene a tick at a time but draws only the frame it stops on: a frame drawn at 1440 px costs a
// loaded runner enough that eight steps of 400 ms ran past a test's 30 s (#961, #976).
test('a film plays every tick of a step and draws only the frame it stops on', async ({ page }) => {
  await film(page, 'start', { spell: 'spell:pummel:v1', targets: ['b1'] });
  expect(await film(page, 'drawn')).toBe(1);
  expect(await film(page, 'advance', WINDUP_MS)).toEqual({ elapsed: WINDUP_MS, stage: 'impact' });
  expect(await film(page, 'drawn')).toBe(2);
  await film(page, 'advance', 20);
  expect(await film(page, 'drawn')).toBe(3);
  await film(page, 'stop');
});

test('less motion shows the settled frame and no effect', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  // Play only after the bench has rested on the media query's change: otherwise the rest undoes the play (#894).
  await expect(page.locator('#lab')).toHaveAttribute('data-motion', 'reduced');
  await page.locator('#bench-play').click();
  await expect(page.locator('#lab')).toHaveAttribute('data-stage', 'resolved');
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

// BC9: the bench stands in a round, and films the field at rest. Found by review: a film of the field at rest was
// titled as the spell the bench had chosen, and a film one round on from where the bench stood started while the
// ground was still giving way, so no two runs agreed.
test('a film of the field at rest is titled as what it is, and a film in the next round is the same film twice', async ({ page }) => {
  test.setTimeout(60000);
  await openBench(page, '/table/battle-lab.html');
  const rest = await filmRecord(page, { still: true, round: 2, frames: 4, interval: 200 });
  expect(rest.motion.title).toBe('The field at rest · round 2 · 1×');
  // Back to the first round, then on to the second again: the arch's span must not be caught mid-fall.
  await filmSheet(page, { still: true, round: 1, frames: 2, interval: 200 });
  const again = await filmRecord(page, { still: true, round: 2, frames: 4, interval: 200 });
  expect(rest.sheet.equals(again.sheet)).toBe(true);
});
