import { test, expect } from '@playwright/test';
import { mkdtemp, rm } from 'node:fs/promises';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { JOURNEYS } from '../../scripts/ux/journeys.mjs';
import { axeSource, walkJourney } from '../../scripts/ux/journey.mjs';
import { readFileSync } from 'node:fs';
import { findingLines, knownLine, unknownFindings } from '../../scripts/ux/access.mjs';
import { MOST_A_SECOND, flashesASecond, luminanceInPage } from '../../scripts/ux/flash.mjs';
import { buildShowcase } from '../../scripts/ux/set.mjs';

// The checks of accessibility the gate runs (docs/art/ux-craft-plan.md, UX13; ADR 0197), on the real engine: every
// screen and state of the journeys, at 320 by 568 and 390 by 844, with nothing found by axe on the rules of WCAG 2.2
// AA, nothing under 7:1 when the system asks for more contrast, no ring, icon or state lost to forced colours, and
// nothing lost with the text spaced (WCAG 1.4.12) or doubled (1.4.4). The walk sets its own size, so the journeys
// are shared between the two projects, each walking its half at both widths.
test.describe.configure({ mode: 'default' });
// About eight minutes each in a session: the lessons alone take five, so the phone also films the flash guard and
// plays the keyboard's round.
const HALVES = { 'table-phone': ['doors', 'lessons'], 'table-desktop': ['lobby', 'pages', 'match', 'windows'] };
const WIDTHS = [{ width: 320, height: 568 }, { width: 390, height: 844 }];
// What UX13 found and left to a later stage, a journey's list each (docs/art/ux-craft-plan.md, UX13's note). A finding not in it fails; one the list holds and the walk no longer finds is said, so the list
// is cut in the change that fixed it. axe is never in it: ADR 0197 holds every screen to none.
const KNOWN = JSON.parse(readFileSync(new URL('../../scripts/ux/access-known.json', import.meta.url), 'utf8'));

let folder, showcase, axe;
test.beforeAll(async () => {
  folder = await mkdtemp(join(tmpdir(), 'ux-access-spec-'));
  showcase = await buildShowcase(join(folder, 'dist'));
  axe = await axeSource();
});
test.afterAll(async () => { if (folder) await rm(folder, { recursive: true, force: true }); });

for (const journey of JOURNEYS) {
  test(`every screen of ${journey.id} holds the checks of accessibility at 320 and 390 px`, async ({ browser }, info) => {
    test.skip(!HALVES[info.project.name]?.includes(journey.id), `${journey.id} is walked by the other project.`);
    test.setTimeout(480000);
    expect(axe, 'axe-core is installed with the browser suite').not.toBeNull();
    const found = [];
    for (const viewport of WIDTHS) {
      const screens = await walkJourney(browser, journey, viewport, { showcase, axe, access: true });
      // Every screen, in the order the walk reached it: a lesson's card can come before its first step.
      expect(screens.map(screen => screen.name).sort()).toEqual([...journey.screens].sort());
      for (const screen of screens) found.push(...findingLines(`${viewport.width} ${screen.name}`, screen.checks));
    }
    const known = KNOWN[journey.id] ?? [];
    expect(known.filter(line => / axe /.test(line)), 'axe is held to none').toEqual([]);
    const gone = known.filter(line => !found.some(one => knownLine(one) === line));
    if (gone.length) info.annotations.push({ type: 'fixed, to cut from access-known.json', description: gone.join('\n') });
    expect(unknownFindings(found, known)).toEqual([]);
  });
}

// The flash guard (WCAG 2.3.1): every window a strip films entering, filmed on the held clock for 1.2 s, flashes
// three times a second at most. The brightest of them is named, so a later window that outshines it is seen.
// friends-window is left out: it closes and opens the window again in one act, so no frame shows it closed.
const ENTERING = [['lobby', 'menu'], ['lobby', 'create-window'], ['match', 'settings-window']];
test('no window that enters flashes more than three times a second', async ({ browser }, info) => {
  test.skip(info.project.name !== 'table-phone', 'The strips set their own size: one project films them.');
  test.setTimeout(300000);
  const phone = { width: 390, height: 844 };
  const read = await browser.newPage();
  const brightness = [];
  try {
    for (const [id, strip] of ENTERING) {
      const journey = JOURNEYS.find(one => one.id === id);
      const shots = await walkJourney(browser, journey, phone, { showcase, strip, frames: 30, interval: 40 });
      const frames = await read.evaluate(luminanceInPage, { pictures: shots.map(shot => shot.png.toString('base64')) });
      const mean = frame => frame.reduce((sum, cell) => sum + cell, 0) / frame.length;
      // A film of nothing would pass: some frame differs from the first (friends-window closes and opens again).
      expect(Math.max(...frames.map(frame => Math.abs(mean(frame) - mean(frames[0])))), `${strip} was filmed moving`).toBeGreaterThan(0.001);
      brightness.push({ strip, mean: mean(frames.at(-1)) });
      expect(flashesASecond(frames, 40), `${strip} flashes`).toBeLessThanOrEqual(MOST_A_SECOND);
    }
  } finally { await read.close(); }
  const brightest = brightness.reduce((one, other) => (other.mean > one.mean ? other : one));
  info.annotations.push({ type: 'brightest window', description: `${brightest.strip}, mean luminance ${brightest.mean.toFixed(3)}` });
});

// The checks see what they are for: a fixture page with one fault of each kind, and its fixed twin with none.
test('the checks find a ring, an icon, a state, a contrast and a clipped word that are lost', async ({ browser }, info) => {
  test.skip(info.project.name !== 'table-phone', 'A fixture page: one project reads it.');
  const { checkScreen } = await import('../../scripts/ux/access.mjs');
  const { AXE_TAGS } = await import('../../scripts/ux/measures.mjs');
  const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
  try {
    const fixture = fixed => `<!doctype html><html lang="en"><head><title>Fixture</title><style>
      body { background: #111; color: #eee; font: 16px sans-serif; }
      button { border: 0; background: #333; color: #eee; min-width: 44px; min-height: 44px; }
      button:focus-visible { outline: none; box-shadow: 0 0 0 3px gold; }
      button[aria-pressed="true"] { background: #640; }
      .muted { color: #8a8a8a; }
      .cut { width: 120px; height: 1.2em; overflow: hidden; }
      ${fixed ? `@media (forced-colors: active) { :focus-visible { outline: 2px solid Highlight !important; }
        [aria-pressed="true"] { text-decoration-line: underline !important; } }
      @media (prefers-contrast: more) { .muted { color: #e0e0e0; } } .cut { height: auto; }
      svg path { fill: currentColor; }` : 'svg path { fill: #c90; }'}
      </style></head><body><main><h1>Fixture</h1>
      <button type="button" aria-pressed="true">One</button><button type="button" aria-pressed="false">Two</button>
      <button type="button" aria-label="Close"><svg width="24" height="24" viewBox="0 0 24 24" aria-hidden="true"><path d="M4 4h16v16H4z"/></svg></button>
      <p class="muted">Some quiet words.</p><p class="cut">Words that are cut when they are spaced</p></main></body></html>`;
    const found = async fixed => {
      await page.setContent(fixture(fixed));
      await page.evaluate(await axeSource());
      return findingLines('fixture', await checkScreen(page, AXE_TAGS));
    };
    const faults = (await found(false)).join('\n');
    for (const kind of ['forced focus', 'forced icons', 'forced state', 'contrast color-contrast-enhanced', 'spacing clipped', 'doubled clipped']) {
      expect(faults, kind).toContain(kind);
    }
    expect(await found(true)).toEqual([]);
  } finally { await page.close(); }
});
