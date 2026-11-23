import { test, expect } from '@playwright/test';
import { mkdtemp, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { encodeFilm, filmFacts, hasEncoder, holdClock, onHost, readShot, root, sha256, shootFilm, shootStill } from '../../scripts/promo/shoot.mjs';
import { startHost } from '../../scripts/ux/set.mjs';

// The promotion's still and film on the real engine (docs/art/promotion-plan.md, P1 and P2; ADR 0205): a shot of
// the repository opened on a real host gives the same bytes twice, and a picture of the size its ratio asks; a film
// of three seconds gives the same frames twice, and an MP4 of the length and size asked. A shot sets its own size,
// so one project plays it.
test.describe.configure({ mode: 'default' });
const shot = readShot(join(root, 'promo/shots/brute-mirror-round-3.json'));

// The width and height a PNG says in its header.
const dimensions = png => [png.readUInt32BE(16), png.readUInt32BE(20)];

test('two stills of one shot give the same bytes, at the size each ratio asks', async ({ browser }, info) => {
  test.skip(info.project.name !== 'table-phone', 'The still sets its own size: one project plays it.');
  test.setTimeout(180000);
  const still = ratio => onHost(startHost, origin => shootStill(browser, origin, shot, ratio));
  const first = await still('9:16'), second = await still('9:16');
  expect(dimensions(first)).toEqual([1080, 1920]);
  expect(first.equals(second)).toBe(true);
  expect(dimensions(await still('16:9'))).toEqual([1920, 1080]);
});

test('the still\'s clock moves only when a tick moves it, not with the wall\'s', async ({ page }, info) => {
  test.skip(info.project.name !== 'table-phone', 'One project is enough: no page of the table is opened.');
  await holdClock(page);
  await page.setContent('<p>held</p>');
  const before = await page.evaluate(() => Date.now());
  // Wall time passes while the still waits for its requests; the page must not see it. The wait can only make a
  // clock that follows the wall's fail, never a held one: it is the thing tested, not a budget.
  await new Promise(done => setTimeout(done, 300));
  expect(await page.evaluate(() => Date.now())).toBe(before);
  await page.clock.runFor(100);
  expect(await page.evaluate(() => Date.now())).toBe(before + 100);
});

test('two takes of a three-second film give the same frames, encoded at the length and size asked', async ({ browser }, info) => {
  test.skip(info.project.name !== 'table-phone', 'The film sets its own size: one project plays it.');
  test.skip(!hasEncoder(), 'ffmpeg and ffprobe are not on the PATH.');
  test.setTimeout(300000);
  // The repository's film shot, cut to three seconds at 720 so the journey stays short: it opens the scenario, closes
  // the first visit's windows and answers the Mutation and the tempo, which is where a take could drift.
  const filmed = readShot(join(root, 'promo/shots/brute-pummel-round-3.json'));
  const shot = { ...filmed, size: 720, film: { ...filmed.film, seconds: 3 } };
  const take = async folder => {
    const hashes = [];
    await onHost(startHost, origin => shootFilm(browser, origin, shot, '9:16', async (index, png) => {
      hashes.push(sha256(png));
      if (folder) await writeFile(join(folder, `${String(index).padStart(6, '0')}.png`), png);
    }));
    return hashes;
  };
  const folder = await mkdtemp(join(tmpdir(), 'promo-film-'));
  try {
    const first = await take(folder), second = await take(null);
    expect(first).toHaveLength(90);
    expect(second).toEqual(first);
    // The film moves: a field at rest for three seconds would pass the comparison above and film nothing.
    expect(new Set(first).size).toBeGreaterThan(1);
    encodeFilm(folder, 30, join(folder, 'film.mp4'));
    const facts = filmFacts(join(folder, 'film.mp4'));
    expect({ width: facts.width, height: facts.height, frames: facts.frames, codec: facts.codec }).toEqual({ width: 720, height: 1280, frames: 90, codec: 'h264' });
    expect(facts.seconds).toBeCloseTo(3, 1);
  } finally {
    await rm(folder, { recursive: true, force: true });
  }
});
