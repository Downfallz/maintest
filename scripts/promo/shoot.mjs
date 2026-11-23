#!/usr/bin/env node
// The still of a shot (docs/art/promotion-plan.md, P1; ADR 0205): what promotion shows as the game is the game,
// filmed. A shot (`promo/shots/*.json`) names a scenario of `scenarios/` and the ratios to frame it at; this
// script raises a real table host, signs in, opens the scenario in Chromium as a player opens it, and saves what
// the battle view draws, one PNG a ratio. The page's clocks and randomness are pinned and the clock moves only
// once the page has what it asked for, so one shot gives the same bytes each time on one machine and one build.
//
//   node scripts/promo/shoot.mjs promo/shots/brute-mirror-round-3.json --still              # artifacts/promo/<shot>/
//   node scripts/promo/shoot.mjs promo/shots/brute-mirror-round-3.json --still --ratio 9:16 --out <folder>
//
// It needs what a journey needs: the engine built, the content consolidated, the battle bundle and the browser
// suite's Playwright (scripts/ux/set.mjs says what is missing).

import { createHash } from 'node:crypto';
import { existsSync, readFileSync } from 'node:fs';
import { mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { basename, dirname, join, relative, resolve, sep } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

export const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
export const FORMAT = 'downfall.promo.shot.v1';

/**
 * The ratios a shot is framed at, each as the screen the page is laid out on, in CSS pixels. The vertical one is a
 * phone's layout, not a crop of the desktop's; the short side is what `size` asks in pixels.
 */
export const RATIOS = Object.freeze({
  '9:16': Object.freeze({ width: 360, height: 640 }),
  '16:9': Object.freeze({ width: 1280, height: 720 }),
  '1:1': Object.freeze({ width: 720, height: 720 }),
});

/** The short side of a picture, in pixels: 1080 for the feeds, 720 for a draft. */
export const SIZES = Object.freeze([720, 1080]);

const KEYS = new Set(['format', 'title', 'scenario', 'ratios', 'size']);

// What the host leaves out of the scenarios it lists (`RepositoryScenarios.cs`): a file directly under
// `scenarios/` named as a tutorial's or a bug's, and a name past the 55 characters an id keeps.
const LONGEST_NAME = 55;
const LEFT_OUT = ['tutorial-', 'bug-'];

/** The id the host gives a scenario file directly under `scenarios/` (scenarios/README.md). */
export function scenarioId(file) {
  return `sc-repo-${basename(file, '.json')}`;
}

/**
 * A shot's file, read and checked: refused when it names a scenario that is not a file directly under
 * `scenarios/`, a ratio this script does not frame, or anything it does not know. Returns the shot with its name.
 */
export function readShot(path, { scenarios = join(root, 'scenarios') } = {}) {
  const name = basename(path, '.json');
  let shot;
  try {
    shot = JSON.parse(readFileSync(path, 'utf8'));
  } catch (error) {
    throw new Error(`${path}: not a shot's JSON (${error.message}).`);
  }
  const refuse = why => { throw new Error(`${path}: ${why}`); };
  if (shot === null || typeof shot !== 'object' || Array.isArray(shot)) refuse('a shot is a JSON object.');
  if (shot.format !== FORMAT) refuse(`"format" is "${FORMAT}".`);
  const unknown = Object.keys(shot).filter(key => !KEYS.has(key));
  if (unknown.length) refuse(`unknown key ${unknown.map(key => JSON.stringify(key)).join(', ')}.`);
  if (typeof shot.title !== 'string' || !shot.title.trim()) refuse('"title" says what the shot shows.');
  // A tutorial or a bug scenario opens with its lesson or its report: a shot plays a scenario of its own.
  if (typeof shot.scenario !== 'string' || !/^[a-z0-9][a-z0-9-]*\.json$/.test(shot.scenario)) {
    refuse('"scenario" names a file directly under scenarios/, such as "greedy-1v1-round-3.json".');
  }
  const named = basename(shot.scenario, '.json');
  if (LEFT_OUT.some(prefix => named.startsWith(prefix))) refuse(`the host leaves out a file directly under scenarios/ named ${LEFT_OUT.map(one => JSON.stringify(`${one}...`)).join(' or ')}.`);
  if (named.length > LONGEST_NAME) refuse(`the host cuts a scenario's name past ${LONGEST_NAME} characters: name the file shorter.`);
  if (!existsSync(join(scenarios, shot.scenario))) refuse(`the scenario scenarios/${shot.scenario} does not exist.`);
  const ratios = shot.ratios ?? Object.keys(RATIOS);
  if (!Array.isArray(ratios) || ratios.length === 0) refuse(`"ratios" lists one or more of ${Object.keys(RATIOS).join(', ')}.`);
  for (const ratio of ratios) if (!Object.hasOwn(RATIOS, ratio)) refuse(`the ratio ${JSON.stringify(ratio)} is not one of ${Object.keys(RATIOS).join(', ')}.`);
  if (new Set(ratios).size !== ratios.length) refuse('"ratios" names a ratio twice.');
  const size = shot.size ?? 1080;
  if (!SIZES.includes(size)) refuse(`"size" is ${SIZES.join(' or ')}.`);
  return { name, title: shot.title, scenario: shot.scenario, id: scenarioId(shot.scenario), ratios, size };
}

/** The page's screen and density for a ratio at a size: the picture is the screen times the density. */
export function framing(ratio, size) {
  const screen = RATIOS[ratio];
  return { viewport: { ...screen }, density: size / Math.min(screen.width, screen.height) };
}

/** A still's file: `still-9x16.png`. */
export const stillName = ratio => `still-${ratio.replace(':', 'x')}.png`;

export function parseArguments(argv) {
  const options = { shot: null, still: false, ratio: null, out: null };
  for (let index = 0; index < argv.length; index++) {
    const word = argv[index];
    if (word === '--still') options.still = true;
    else if (word === '--ratio' || word === '--out') {
      if (index + 1 >= argv.length) throw new Error(`${word} needs a value.`);
      options[word.slice(2)] = argv[++index];
    } else if (word.startsWith('--')) throw new Error(`Unknown option ${word}.`);
    else if (options.shot) throw new Error('Name one shot.');
    else options.shot = word;
  }
  if (!options.shot) throw new Error('Usage: node scripts/promo/shoot.mjs promo/shots/<shot>.json --still [--ratio 9:16] [--out <folder>]');
  // The film of a stretch of a match is P2's: this stage takes stills.
  if (!options.still) throw new Error('Only --still is shot today: one frame at each ratio.');
  if (options.ratio !== null && !Object.hasOwn(RATIOS, options.ratio)) throw new Error(`--ratio is one of ${Object.keys(RATIOS).join(', ')}.`);
  return options;
}

// Math.random, seeded: the page draws the same scatter each time. A linear congruential generator is enough for
// pictures; nothing here is a secret.
function pinRandom() {
  let state = 7;
  Math.random = () => { state = (Math.imul(state, 1664525) + 1013904223) >>> 0; return state / 4294967296; };
}

// The moment the page's clock starts from: fixed, so whatever the page dates is dated the same.
const EPOCH = new Date('2026-01-01T00:00:00Z');
const TICK_MS = 100;

/**
 * Run `step` until `done` says so, at most `times` times, one after the other: each step acts on what the last one
 * left. `done` is asked before each step and once after the last; it is the answer.
 */
export async function steps(step, { times, done = () => false }) {
  if (await done()) return true;
  if (times <= 0) return false;
  await step();
  return steps(step, { times: times - 1, done });
}

/**
 * Give the page a clock that moves only when it is told to: installed at EPOCH, a clock still follows the wall's,
 * so it is paused at once. Call it before the page loads.
 */
export async function holdClock(page) {
  await page.clock.install({ time: EPOCH });
  await page.clock.pauseAt(new Date(EPOCH.getTime() + TICK_MS));
}

/**
 * Open the shot's scenario on the host at `origin` in a new context framed at `ratio`, and return the PNG of the
 * screen. Give it a host of its own: an account that opened the scenario before would resume its practice.
 */
export async function shootStill(browser, origin, shot, ratio) {
  const { viewport, density } = framing(ratio, shot.size);
  // Less motion: the field at rest, as a player who asks for it sees it. Its embers and its breath follow the
  // time since the view arrived, which no pinned clock holds the same from one take to the next.
  const context = await browser.newContext({ viewport, deviceScaleFactor: density, reducedMotion: 'reduce', locale: 'en-US', timezoneId: 'UTC' });
  try {
    const page = await context.newPage();
    // What the page is still waiting for, but the event streams, which stay open for as long as the page does.
    const open = new Set(), waiting = [];
    page.on('request', request => { if (request.resourceType() !== 'eventsource' && !request.url().endsWith('/events')) open.add(request); });
    const done = request => {
      open.delete(request);
      if (open.size === 0) for (const answered of waiting.splice(0)) answered();
    };
    page.on('requestfinished', done);
    page.on('requestfailed', done);
    // Every request the page has made is answered: a request that never is fails the still, not its bytes.
    const settled = () => open.size === 0 ? Promise.resolve() : new Promise((answered, fail) => {
      const late = setTimeout(() => fail(new Error(`The page still waits for ${[...open].map(one => one.url()).join(', ')}.`)), 30000);
      waiting.push(() => { clearTimeout(late); answered(); });
    });
    // A request the page has made may not have reached this process yet: one round trip to the page first.
    const caughtUp = () => page.evaluate(() => 0).then(settled);
    // The page's clock moves only here, a tick at a time, once what the last tick asked for has come.
    const tick = (times = 1) => steps(() => page.clock.runFor(TICK_MS).then(caughtUp), { times });
    await page.addInitScript(pinRandom);
    await holdClock(page);
    const signed = await page.request.post(`${origin}/api/account/dev`, { data: { username: 'Promo' } });
    if (!signed.ok()) throw new Error(`The host refused the account: ${signed.status()}.`);
    await page.goto(`${origin}/battle?scenario=${encodeURIComponent(shot.id)}`);
    const ready = page.locator('#battle[data-ready="true"]');
    const opened = await steps(tick, { times: 300, done: async () => await ready.count() > 0 });
    if (!opened) throw new Error(`The battle view did not open the scenario ${shot.scenario}.`);
    await tick(30);
    // The practice's briefing is begun, and the windows a first visit opens are closed, as a player closes them.
    const begin = page.locator('#practice-brief-begin');
    if (await begin.isVisible()) { await begin.click(); await tick(10); }
    const closed = await steps(() => page.keyboard.press('Escape').then(() => tick(10)),
      { times: 6, done: async () => await page.locator('dialog[open]').count() === 0 });
    if (!closed) throw new Error('A window stayed open over the field.');
    // The practice's band says this is a practice: a match has none, and a post shows the match.
    if (await page.locator('#practice-band').count() !== 1) throw new Error('The practice\'s band (#practice-band) is not on the page: the still would show what it hides.');
    await page.addStyleTag({ content: '#practice-band { display: none !important; }' });
    await tick(30);
    return await page.screenshot();
  } finally {
    await context.close();
  }
}

/** Raise a table host in a folder of its own, run `work` with its origin, and take both down. */
export async function onHost(startHost, work) {
  const folder = await mkdtemp(join(tmpdir(), 'promo-shoot-'));
  let host;
  try {
    host = await startHost(folder);
    return await work(host.origin);
  } finally {
    await host?.close();
    await rm(folder, { recursive: true, force: true });
  }
}

const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');

async function main(argv) {
  const options = parseArguments(argv);
  const shot = readShot(resolve(options.shot));
  if (options.ratio && !shot.ratios.includes(options.ratio)) throw new Error(`--ratio ${options.ratio} is not one of the shot's: ${shot.ratios.join(', ')}.`);
  const ratios = options.ratio ? [options.ratio] : shot.ratios;
  // Loaded here, not at the top: the checks above need no browser and no engine.
  const { missing, startHost } = await import('../ux/set.mjs');
  const { playwright } = await import('../battle/filmstrip.mjs');
  const absent = missing();
  if (absent.length) throw new Error(absent.join('\n'));
  const folder = resolve(options.out ?? join(root, 'artifacts/promo', shot.name));
  await mkdir(folder, { recursive: true });
  const { chromium } = playwright();
  const browser = await chromium.launch({ executablePath: process.env.STUDIO_CHROMIUM || undefined });
  try {
    // One ratio after the other, each on a host of its own.
    const shootOne = async ratio => {
      const png = await onHost(startHost, origin => shootStill(browser, origin, shot, ratio));
      const path = join(folder, stillName(ratio));
      await writeFile(path, png);
      const shown = path.startsWith(`${root}${sep}`) ? relative(root, path) : path;
      console.log(`${shown}  sha256:${sha256(png)}`);
    };
    await ratios.reduce((previous, ratio) => previous.then(() => shootOne(ratio)), Promise.resolve());
  } finally {
    await browser.close();
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    await main(process.argv.slice(2));
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}
