// The pages' picture kit as a browser shows it (ADR 0198, table/picture.js, table/icons.js, table/identity.css):
// a picture holds its room and takes the file its width asks for, a strip steps its frames once while it is the
// screen's ambience and is its first frame under less motion, and an icon takes the colour of its text. The
// page is read as the host serves it, with no script but the kit's own modules.
import { test, expect } from '@playwright/test';

const KIT = ['/motion.js', '/picture.js', '/picture-focus.js', '/page-art.js', '/icons.js'];

async function open(page) {
  await page.route('**/*.js', route => (KIT.some(name => new URL(route.request().url()).pathname.endsWith(name)) ? route.continue() : route.abort()));
  await page.route('**/api/**', route => route.fulfill({ status: 404, json: { message: 'Not here.' } }));
  await page.goto('/table/rules.html');
}

/** A strip of four frames, 80 px each, made in the page: no strip is committed until a journey films one. */
const addStrip = page => page.evaluate(async () => {
  const canvas = Object.assign(document.createElement('canvas'), { width: 320, height: 40 });
  const pen = canvas.getContext('2d');
  ['#111', '#555', '#999', '#ddd'].forEach((shade, frame) => { pen.fillStyle = shade; pen.fillRect(frame * 80, 0, 80, 40); });
  const { stripElement } = await import('./picture.js');
  const art = { cast: { frames: 4, drawn: [80], pages: { here: 'lazy' }, sizes: [{ file: 'cast.webp', width: 320, height: 40, bytes: 1 }] } };
  const strip = stripElement(document, 'cast', { page: 'here', art, alt: 'A creature casts, and the blow lands.' });
  const image = strip.querySelector('img');
  image.removeAttribute('srcset');
  image.removeAttribute('loading');
  image.src = canvas.toDataURL();
  strip.id = 'kit-strip';
  strip.style.cssText = 'position: absolute; top: 10px; left: 0; width: 80px;';
  document.body.append(strip);
  await image.decode();
  (await import('./motion.js')).mountMotion().watch();
});

/** How far the strip's picture has stepped, in frames, and whether anything still plays. */
const stripAt = page => page.evaluate(() => {
  const strip = document.getElementById('kit-strip'), image = strip.querySelector('img');
  const frame = Math.round((strip.getBoundingClientRect().left - image.getBoundingClientRect().left) / strip.getBoundingClientRect().width);
  return { frame, playing: image.getAnimations().some(animation => animation.playState === 'running'), box: [strip.offsetWidth, strip.offsetHeight] };
});

test('a strip holds one frame of room, steps to its last frame once and rests there', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await open(page);
  await addStrip(page);
  await expect.poll(() => stripAt(page)).toEqual({ frame: 3, playing: false, box: [80, 40] });
  await expect(page.locator('#kit-strip')).toHaveAttribute('data-ambient', 'playing');
  await expect(page.getByRole('img', { name: 'A creature casts, and the blow lands.' })).toHaveCount(1);
  // Only a transform moved it.
  const moved = await page.evaluate(() => document.querySelector('#kit-strip img').getAnimations().flatMap(animation => animation.effect.getKeyframes().flatMap(frame => Object.keys(frame)))
    .filter(name => !['offset', 'easing', 'composite', 'computedOffset'].includes(name)));
  expect([...new Set(moved)]).toEqual(['translate']);
});

test('a strip out of view goes back to its first frame, and plays again when it returns', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await open(page);
  await addStrip(page);
  await expect.poll(async () => (await stripAt(page)).frame).toBe(3);
  await page.evaluate(() => { document.getElementById('kit-strip').style.top = '400vh'; });
  await expect.poll(async () => (await stripAt(page)).frame).toBe(0);
  await page.evaluate(() => { document.getElementById('kit-strip').style.top = '10px'; });
  await expect.poll(async () => (await stripAt(page)).frame).toBe(3);
});

for (const [name, less] of [['the system', { media: 'reduce' }], ['the account', { media: 'no-preference', saved: true }]]) {
  test(`under less motion asked by ${name}, a strip is its first frame, still`, async ({ page }) => {
    await page.emulateMedia({ reducedMotion: less.media });
    if (less.saved) await page.addInitScript(() => localStorage.setItem('downfall.arena.game-settings.v1', JSON.stringify({ reducedMotion: true })));
    await open(page);
    await addStrip(page);
    await expect(page.locator('#kit-strip')).toHaveAttribute('data-ambient', 'playing');
    expect(await stripAt(page)).toEqual({ frame: 0, playing: false, box: [80, 40] });
  });
}

test('a picture of the kit holds its room before it loads and takes the file its width asks for', async ({ page }) => {
  await open(page);
  const taken = await page.evaluate(async () => {
    const { pictureElement } = await import('./picture.js');
    const image = pictureElement(document, 'colossus', { page: 'showcase/home', base: 'page-art/' });
    image.style.cssText = 'position: fixed; top: 0; left: 0; display: block; width: 308px;';
    const room = (document.body.append(image), [image.offsetWidth, image.offsetHeight]);
    await image.decode();
    return { room, file: new URL(image.currentSrc).pathname, natural: image.naturalWidth, priority: image.getAttribute('fetchpriority') };
  });
  expect(taken.room).toEqual([308, 205]);
  const density = await page.evaluate(() => devicePixelRatio);
  expect(taken.natural).toBe(density >= 2 ? 620 : 308);
  expect(taken.file).toMatch(/^\/table\/page-art\/colossus-\d+\.[0-9a-f]{8}\.webp$/);
  expect(taken.priority).toBe('high');
});

test('a picture wider than the screen is held to it, and the page does not scroll sideways', async ({ page }) => {
  await open(page);
  const held = await page.evaluate(async () => {
    const { pictureElement } = await import('./picture.js');
    const image = pictureElement(document, 'eclipse-ground', { page: 'table/sign-in', base: 'page-art/', sizes: '100vw' });
    image.style.display = 'block';
    const room = Object.assign(document.createElement('div'), { id: 'kit-room' });
    room.append(image);
    document.body.prepend(room);
    return { fits: image.offsetWidth <= room.clientWidth, ratio: image.offsetWidth / image.offsetHeight, sideways: document.documentElement.scrollWidth > document.documentElement.clientWidth };
  });
  expect(held.fits).toBe(true);
  expect(held.ratio).toBeCloseTo(1672 / 941, 1);
  expect(held.sideways).toBe(false);
});

test('an icon takes the colour and the size of its text, and says nothing of its own', async ({ page }) => {
  await open(page);
  const drawn = await page.evaluate(async () => {
    const { iconElement } = await import('./icons.js');
    const button = Object.assign(document.createElement('button'), { type: 'button' });
    button.setAttribute('aria-label', 'Close settings');
    button.style.cssText = 'position: fixed; top: 0; left: 0; z-index: 99; min-width: 44px; min-height: 44px; font-size: 16px; color: rgb(1, 2, 3);';
    button.append(iconElement(document, 'close'));
    document.body.append(button);
    const icon = button.querySelector('svg'), style = getComputedStyle(icon);
    return { stroke: style.stroke, fill: style.fill, size: [icon.getBoundingClientRect().width, icon.getBoundingClientRect().height] };
  });
  expect(drawn).toEqual({ stroke: 'rgb(1, 2, 3)', fill: 'none', size: [20, 20] });
  await expect(page.getByRole('button', { name: 'Close settings' })).toHaveCount(1);
});
