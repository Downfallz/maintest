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

/**
 * A scene of each form, its painting a white canvas made in the page: the worst painting there is under bone
 * words. `scrim: false` takes the scrim away, to show what the measure finds without it.
 */
const addScene = (page, form, { scrim = true, saveData = false, side } = {}) => page.evaluate(async ({ form, scrim, saveData, side }) => {
  const canvas = Object.assign(document.createElement('canvas'), { width: 900, height: 300 });
  const pen = canvas.getContext('2d');
  pen.fillStyle = '#fff'; pen.fillRect(0, 0, 900, 300);
  const { sceneElement } = await import('./picture.js');
  const art = { pool: { frames: 1, drawn: [358], pages: { here: 'first' }, sizes: [{ file: 'pool.webp', width: 900, height: 300, bytes: 1 }] } };
  const eyebrow = Object.assign(document.createElement('p'), { className: 'eyebrow', textContent: 'Friends' });
  const title = Object.assign(document.createElement('h2'), { textContent: 'Invite a friend' });
  const scene = sceneElement(document, 'pool', { page: 'here', art, form, side, saveData, alt: form === 'plate' ? 'A pool.' : undefined, words: form === 'plate' ? ['See you soon.'] : [eyebrow, title] });
  const image = scene.querySelector('img');
  if (image) { image.removeAttribute('srcset'); image.src = canvas.toDataURL(); }
  scene.id = 'kit-scene';
  document.body.replaceChildren(scene);
  if (!scrim) document.head.append(Object.assign(document.createElement('style'), { textContent: '#kit-scene::before { content: none; }' }));
  await image?.decode();
  const box = scene.getBoundingClientRect();
  return { width: Math.round(box.width), height: Math.round(box.height), images: scene.querySelectorAll('img').length };
}, { form, scrim, saveData, side });

test('a head is a 3:1 band no taller than 120 px, and its words hold 4.5:1 on its scrim over the lightest painting', async ({ page }) => {
  const { measureContrast } = await import('../../scripts/ux/contrast.mjs');
  await open(page);
  for (const side of ['start', 'end']) {
    const drawn = await addScene(page, 'head', { side });
    expect(drawn.height).toBe(Math.min(120, Math.round(drawn.width / 3)));
    const contrast = await measureContrast(page);
    expect(contrast.texts).toBe(2);
    expect(contrast.under, `${side}: ${JSON.stringify(contrast)}`).toBe(0);
  }
});

test('the measure finds words on a painting with no scrim, and says nothing of a screen with none', async ({ page }) => {
  const { measureContrast } = await import('../../scripts/ux/contrast.mjs');
  await open(page);
  expect(await measureContrast(page)).toBeNull();
  await addScene(page, 'head', { scrim: false });
  const contrast = await measureContrast(page);
  expect(contrast.under).toBe(2);
  expect(contrast.lowest).toBeLessThan(2);
});

test('on a short screen a head shrinks to 64 px', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 568 });
  await open(page);
  expect((await addScene(page, 'head')).height).toBe(64);
});

test('a ground holds its words on its dimmed painting, and goes under less data', async ({ page }) => {
  const { measureContrast } = await import('../../scripts/ux/contrast.mjs');
  await open(page);
  expect((await addScene(page, 'ground')).images).toBe(1);
  expect((await measureContrast(page)).under).toBe(0);
  expect((await addScene(page, 'ground', { saveData: true })).images).toBe(0);
  expect(await measureContrast(page), 'a ground not drawn lays no words on a painting').toBeNull();
});

test('a plate is its painting whole, named, with its words under it', async ({ page }) => {
  await open(page);
  const drawn = await addScene(page, 'plate');
  await expect(page.getByRole('figure', { name: 'See you soon.' }).getByRole('img', { name: 'A pool.' })).toHaveCount(1);
  const picture = await page.locator('#kit-scene img').boundingBox();
  expect(Math.round(picture.width / picture.height)).toBe(3);
  expect(drawn.height).toBeGreaterThan(picture.height);
});
