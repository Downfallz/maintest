import { test, expect } from '@playwright/test';
import { mkdtemp, rm } from 'node:fs/promises';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { JOURNEYS } from '../../scripts/ux/journeys.mjs';
import { axeSource, walkJourney } from '../../scripts/ux/journey.mjs';
import { TARGET, TARGET_MINIMUM, TEXT_FLOOR, differences, measureInPage } from '../../scripts/ux/measures.mjs';
import { buildShowcase } from '../../scripts/ux/set.mjs';

// The journey film on the real engine (docs/art/ux-craft-plan.md, UX2): the walk reaches every screen it names,
// and two walks of one journey give the same measures. The two doors are walked, the one journey that crosses
// the showcase and the game. The walk sets its own phone size, so one project plays it.
test.describe.configure({ mode: 'default' });
const phone = { width: 390, height: 844 };
const doors = JOURNEYS.find(journey => journey.id === 'doors');

let folder, showcase;
test.beforeAll(async ({}, info) => {
  // Built for the one project that walks: the other skips both tests.
  if (info.project.name !== 'table-phone') return;
  folder = await mkdtemp(join(tmpdir(), 'ux-journey-spec-'));
  showcase = await buildShowcase(join(folder, 'dist'));
});
test.afterAll(async () => { if (folder) await rm(folder, { recursive: true, force: true }); });

test('two walks of the two doors reach every screen and measure the same', async ({ browser }, info) => {
  test.skip(info.project.name !== 'table-phone', 'The walk sets its own size: one project plays it.');
  test.setTimeout(180000);
  const axe = await axeSource();
  expect(axe, 'axe-core is installed with the browser suite').not.toBeNull();
  const first = await walkJourney(browser, doors, phone, { showcase, axe });
  const second = await walkJourney(browser, doors, phone, { showcase, axe });
  expect(first.map(screen => screen.name)).toEqual(doors.screens);
  // Every press of the doors is one a player can make, on either walk.
  expect(first.blocked).toEqual([]);
  expect(second.blocked).toEqual([]);
  expect(differences(first, second)).toEqual([]);
  // A measure that reads nothing would agree with itself too.
  const home = first[0].measures;
  expect(home.words).toBeGreaterThan(100);
  expect(home.targets.all).toBeGreaterThan(5);
  expect(home.pictures.img + home.pictures.background).toBeGreaterThan(0);
  expect(home.axe).toBeDefined();
});

test('a strip films one interaction on a held clock, the same frames twice', async ({ browser }, info) => {
  test.skip(info.project.name !== 'table-phone', 'The walk sets its own size: one project plays it.');
  test.setTimeout(120000);
  const lobby = JOURNEYS.find(journey => journey.id === 'lobby');
  const film = () => walkJourney(browser, lobby, phone, { showcase, strip: 'menu', frames: 6, interval: 40 });
  const first = await film(), second = await film();
  expect(first).toHaveLength(6);
  expect(second).toHaveLength(6);
  expect(first.map(shot => shot.animating)).toEqual(second.map(shot => shot.animating));
  // The menu is open on the last frame and closed on the first: the interaction was filmed, not its aftermath.
  expect(first[0].png.equals(first.at(-1).png)).toBe(false);
  expect(first.at(-1).png.equals(second.at(-1).png)).toBe(true);
});

// A picture is measured by the file the browser chose, not by `naturalWidth`, which a srcset divides by its density
// (#966): a file over twice its drawn width, or under it, is counted whatever `sizes` says; a drawing never is.
test('the measure counts a picture sent over twice its size or under it, by the file the srcset chose', async ({ browser }, info) => {
  test.skip(info.project.name !== 'table-phone', 'A fixture page at its own density: one project reads it.');
  const context = await browser.newContext({ viewport: { width: 1000, height: 800 }, deviceScaleFactor: 1 });
  try {
    const page = await context.newPage();
    const file = async width => {
      await page.setContent(`<div style="width:${width}px;height:40px;background:#a33"></div>`);
      return page.locator('div').screenshot();
    };
    const files = { 'big.png': await file(800), 'small.png': await file(50), 'right.png': await file(400) };
    const drawing = '<svg xmlns="http://www.w3.org/2000/svg" width="2000" height="400"><rect width="2000" height="400" fill="#3a3"/></svg>';
    await page.route('http://fixture.test/**', route => {
      const name = new URL(route.request().url()).pathname.slice(1);
      if (name === 'drawing.svg') return route.fulfill({ contentType: 'image/svg+xml', body: drawing });
      if (files[name]) return route.fulfill({ contentType: 'image/png', body: files[name] });
      return route.fulfill({ contentType: 'text/html', body: `<!doctype html><body style="margin:0">
        <img alt="" srcset="/big.png 800w" sizes="150px" style="width:100px;display:block">
        <img alt="" srcset="/big.png 2x" style="width:100px;display:block">
        <img alt="" srcset="/small.png 50w" sizes="300px" style="width:300px;display:block">
        <img alt="" srcset="/right.png 400w" sizes="200px" style="width:200px;display:block">
        <img alt="" src="/drawing.svg" style="width:50px;display:block">` });
    });
    await page.goto('http://fixture.test/');
    await page.waitForFunction(() => [...document.images].every(image => image.complete && image.naturalWidth > 0));
    const { pictures } = await page.evaluate(measureInPage, { floor: TEXT_FLOOR, target: TARGET, minimum: TARGET_MINIMUM });
    expect(pictures).toMatchObject({ img: 5, oversized: 2, undersized: 1 });
  } finally {
    await context.close();
  }
});
