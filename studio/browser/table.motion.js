import { test, expect } from '@playwright/test';
import { mkdtemp, rm } from 'node:fs/promises';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { JOURNEYS } from '../../scripts/ux/journeys.mjs';
import { walkJourney } from '../../scripts/ux/journey.mjs';
import { TRAVEL, lessMotionFaults, takeTravelInPage, watchTravelInPage } from '../../scripts/ux/motion.mjs';
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

// The watch sees each way a page can move a thing, and nothing else: a transition, a keyframe animation and a call to
// `animate` that travel are kept; a fade and the classic sheet's net, a travel cut to 0.01 ms, are not.
test('the watch keeps what travels and leaves a fade and a cut duration', async ({ browser }, info) => {
  test.skip(info.project.name !== 'table-phone', 'A fixture page: one project reads it.');
  const context = await browser.newContext({ reducedMotion: 'reduce' });
  try {
    await context.addInitScript(watchTravelInPage, TRAVEL);
    const page = await context.newPage();
    // A page loaded, as a walk loads one: `setContent` rewrites the document, and the window's listeners with it.
    await page.route('http://fixture.test/**', route => route.fulfill({ contentType: 'text/html', body: `<!doctype html><style>
      @keyframes rise { from { transform: translateY(8px); } }
      @keyframes fade { from { opacity: 0; } }
      #slide { transition: translate 200ms; } #slide.on { translate: 0 20px; }
      #cut { transition: transform 0.01ms; } #cut.on { transform: scale(2); }
      #rise.on { animation: rise 200ms; } #fade.on { animation: fade 200ms; }
    </style><div id="slide">a</div><div id="cut">b</div><div id="rise">c</div><div id="fade">d</div><div id="spin">e</div>` }));
    await page.goto('http://fixture.test/');
    await page.evaluate(() => new Promise(done => requestAnimationFrame(() => {
      for (const id of ['slide', 'cut', 'rise', 'fade']) document.getElementById(id).classList.add('on');
      document.getElementById('spin').animate([{ rotate: '0deg' }, { rotate: '360deg' }], { duration: 300 });
      document.getElementById('spin').animate([{ opacity: 0 }, { opacity: 1 }], { duration: 300 });
      setTimeout(done, 100);
    })));
    expect(await page.evaluate(takeTravelInPage)).toEqual(['#rise animation rise transform', '#slide transition translate', '#spin animate rotate']);
    expect(await page.evaluate(takeTravelInPage), 'a read forgets what it read').toEqual([]);
  } finally {
    await context.close();
  }
});
