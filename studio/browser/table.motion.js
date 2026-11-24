import { test, expect } from '@playwright/test';
import { mkdtemp, rm } from 'node:fs/promises';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { JOURNEYS } from '../../scripts/ux/journeys.mjs';
import { walkJourney } from '../../scripts/ux/journey.mjs';
import { lessMotionFaults, travelWatchedInPage, watchTravel } from '../../scripts/ux/motion.mjs';
import { buildShowcase } from '../../scripts/ux/set.mjs';

// Less motion across every screen (docs/art/ux-craft-plan.md, UX14; ADR 0197), on the real engine: every journey
// walked at 320 by 568 and 390 by 844 with the system's wish for less motion. On the way to each screen nothing
// moves across the screen (a transform, a translate, a scale or a rotate that lasts), and at rest nothing animates:
// travel becomes a fade, and ambience stops. The walk sets its own size, so the journeys are shared between the two
// projects as the checks of accessibility share them (table.access.js).
test.describe.configure({ mode: 'default' });
const HALVES = { 'table-phone': ['doors', 'lessons'], 'table-desktop': ['lobby', 'pages', 'match', 'windows'] };
const WIDTHS = [{ width: 320, height: 568 }, { width: 390, height: 844 }];

let folder, showcase;
test.beforeAll(async () => {
  folder = await mkdtemp(join(tmpdir(), 'ux-motion-spec-'));
  showcase = await buildShowcase(join(folder, 'dist'));
});
test.afterAll(async () => { if (folder) await rm(folder, { recursive: true, force: true }); });

for (const journey of JOURNEYS) {
  test(`under less motion nothing on ${journey.id} travels, and nothing animates at rest, at 320 and 390 px`, async ({ browser }, info) => {
    test.skip(!HALVES[info.project.name]?.includes(journey.id), `${journey.id} is walked by the other project.`);
    test.setTimeout(600000);
    const faults = [];
    for (const viewport of WIDTHS) {
      const screens = await walkJourney(browser, journey, viewport, { showcase, lessMotion: true });
      faults.push(...lessMotionFaults(screens).map(line => `${viewport.width} ${line}`));
    }
    expect(faults).toEqual([]);
  });
}

// The control: the same walk and watch with full motion see the lobby's windows rise, so a walk that finds nothing under
// less motion found nothing because nothing travelled, not because the watch was blind.
test('with full motion the same watch sees the lobby travel', async ({ browser }, info) => {
  test.skip(info.project.name !== 'table-desktop', 'The lobby is walked by the desktop project.');
  test.setTimeout(300000);
  const full = { newContext: options => browser.newContext({ ...options, reducedMotion: 'no-preference' }) };
  const lobby = JOURNEYS.find(journey => journey.id === 'lobby');
  const screens = await walkJourney(full, lobby, WIDTHS[1], { showcase, lessMotion: true });
  expect(lessMotionFaults(screens).filter(line => / (transition|animation|animate) /.test(line)).length).toBeGreaterThan(0);
});

// A walk under less motion stops on a screen whose page the watch is not on: nothing read there could be called still.
test('a walk under less motion stops on a page without the watch', async ({ browser }, info) => {
  test.skip(info.project.name !== 'table-phone', 'A stub journey: one project walks it.');
  test.setTimeout(120000);
  const bare = await browser.newContext();
  try {
    const stub = {
      id: 'stub', title: 'A stub', screens: ['bare'],
      async walk(walk) {
        const page = await bare.newPage();
        await page.goto('about:blank');
        await walk.screen('bare', { on: page });
      },
    };
    await expect(walkJourney(browser, stub, WIDTHS[1], { showcase, lessMotion: true })).rejects.toThrow(/the watch of less motion is not on this page/);
  } finally {
    await bare.close();
  }
});

// The watch sees each way a page can move a thing, and nothing else: a transition, a keyframe animation and a call to
// `animate` that travel, on an element or on its `::before` and `::after`, and a smooth scroll are kept; a fade and the
// classic sheet's net, a travel cut to 0.01 ms, are not. What travelled leaves the page at once, so a navigation loses
// nothing, and a page the watch is not on says so.
test('the watch keeps what travels, on a pseudo-element too, and leaves a fade and a cut duration', async ({ browser }, info) => {
  test.skip(info.project.name !== 'table-phone', 'A fixture page: one project reads it.');
  const context = await browser.newContext({ reducedMotion: 'reduce' });
  const blind = await browser.newContext();
  try {
    const watch = await watchTravel(context);
    const page = await context.newPage();
    // A page loaded, as a walk loads one: `setContent` rewrites the document, and the window's listeners with it.
    await page.route('http://fixture.test/**', route => route.fulfill({ contentType: 'text/html', body: `<!doctype html><style>
      @keyframes rise { from { transform: translateY(8px); } }
      @keyframes fade { from { opacity: 0; } }
      #slide { transition: translate 200ms; } #slide.on { translate: 0 20px; }
      #cut { transition: transform 0.01ms; } #cut.on { transform: scale(2); }
      #listed { transition: transform 200ms, all 0.01ms; } #listed.on { transform: scale(2); }
      #rise.on { animation: rise 200ms; } #fade.on { animation: fade 200ms; }
      #before::before, #after::after { content: '·'; display: inline-block; }
      #before.on::before { animation: rise 200ms; }
      #after::after { transition: transform 200ms; } #after.on::after { transform: scale(2); }
    </style><div id="slide">a</div><div id="cut">b</div><div id="listed">i</div><div id="rise">c</div><div id="fade">d</div><div id="spin">e</div>
    <div id="before">f</div><div id="after">g</div><div style="height:3000px"></div><p id="far">h</p>` }));
    await page.goto('http://fixture.test/');
    expect(await page.evaluate(travelWatchedInPage)).toBe(true);
    await page.evaluate(() => new Promise(done => requestAnimationFrame(() => {
      for (const id of ['slide', 'cut', 'listed', 'rise', 'fade', 'before', 'after']) document.getElementById(id).classList.add('on');
      document.getElementById('spin').animate([{ rotate: '0deg' }, { rotate: '360deg' }], { duration: 300 });
      document.getElementById('spin').animate([{ opacity: 0 }, { opacity: 1 }], { duration: 300 });
      document.getElementById('far').scrollIntoView({ behavior: 'smooth' });
      done();
    })));
    // What the watch kept reaches the walk when the page's events fire: waited for, never timed (studio/AGENTS.md).
    const seen = new Set();
    await expect.poll(() => { for (const line of watch.take()) seen.add(line); return [...seen].sort(); }).toEqual([
      '#after::after transition transform', '#before::before animation rise transform', '#far scrollIntoView smooth',
      '#rise animation rise transform', '#slide transition translate', '#spin animate rotate']);
    // Then the page goes: what it kept was already out of it, and a take forgets what it took.
    await page.goto('http://fixture.test/again');
    expect(watch.take()).toEqual([]);
    const unwatched = await blind.newPage();
    await unwatched.goto('about:blank');
    expect(await unwatched.evaluate(travelWatchedInPage), 'a page without the watch is not read as still').toBe(false);
  } finally {
    await context.close();
    await blind.close();
  }
});
