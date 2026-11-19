import { test, expect } from '@playwright/test';
import { mkdtemp, rm } from 'node:fs/promises';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { JOURNEYS } from '../../scripts/ux/journeys.mjs';
import { axeSource, walkJourney } from '../../scripts/ux/journey.mjs';
import { differences } from '../../scripts/ux/measures.mjs';
import { buildShowcase } from '../../scripts/ux/set.mjs';

// The journey film on the real engine (docs/art/ux-craft-plan.md, UX2): the walk reaches every screen it names,
// and two walks of one journey give the same measures. The two doors are walked, the one journey that crosses
// the showcase and the game. The walk sets its own phone size, so one project plays it.
test.describe.configure({ mode: 'default' });
const phone = { width: 390, height: 844 };
const doors = JOURNEYS.find(journey => journey.id === 'doors');

let folder, showcase;
test.beforeAll(async () => {
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
  // Every press of the doors is one a player can make.
  expect(first.blocked).toEqual([]);
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
  expect(first.map(shot => shot.ms)).toEqual([0, 40, 80, 120, 160, 200]);
  expect(first.map(shot => shot.animating)).toEqual(second.map(shot => shot.animating));
  // The menu is open on the last frame and closed on the first: the interaction was filmed, not its aftermath.
  expect(first[0].png.equals(first.at(-1).png)).toBe(false);
  expect(first.at(-1).png.equals(second.at(-1).png)).toBe(true);
});
