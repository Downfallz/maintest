#!/usr/bin/env node
// The still and the film of a shot (docs/art/promotion-plan.md, P1 and P2; ADR 0205): what promotion shows as the
// game is the game, filmed. A shot (`promo/shots/*.json`) names a scenario of `scenarios/` and the ratios to frame
// it at; this script raises a real table host, signs in, opens the scenario in Chromium as a player opens it, and
// saves what the battle view draws: one PNG a ratio for a still, a frame at a time and an MP4 for a film. The
// page's clocks and randomness are pinned and the clock moves only once the page has what it asked for, so one
// shot gives the same bytes each time on one machine and one build.
//
//   node scripts/promo/shoot.mjs promo/shots/brute-mirror-round-3.json --still              # artifacts/promo/<shot>/
//   node scripts/promo/shoot.mjs promo/shots/brute-mirror-round-3.json --still --ratio 9:16 --out <folder>
//   node scripts/promo/shoot.mjs promo/shots/brute-mirror-round-3.json --film --ratio 9:16  # frames, MP4, sheet
//
// It needs what a journey needs: the engine built, the content consolidated, the battle bundle and the browser
// suite's Playwright (scripts/ux/set.mjs says what is missing); a film also needs `ffmpeg` on the PATH.

import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { basename, dirname, join, relative, resolve, sep } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

export const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');

/**
 * Today in Montreal, where the owner makes and posts: the one day the promotion's scripts check a date against, so a
 * file taken in this evening and a post published this evening are not dated tomorrow, as UTC would have them.
 */
export const montrealToday = (now = new Date()) => new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Toronto' }).format(now);
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

/** A film's frame rates, and its longest length in seconds: a feed's clip, not a match. */
export const FRAME_RATES = Object.freeze([30, 60]);
export const LONGEST_FILM = 60;

/**
 * The margins a feed covers with its buttons and captions, as fractions of the picture, by ratio (the plan's
 * "safe zones"): the widest of what YouTube Shorts, TikTok and Instagram Reels publish for a 1080 by 1920 clip,
 * rounded up. They are drawn on a film's sheet; nothing the game shows is moved for them. The horizontal and the
 * square clips play in a player that covers nothing while they play.
 */
export const SAFE_ZONES = Object.freeze({
  '9:16': Object.freeze({ top: 250 / 1920, bottom: 480 / 1920, left: 60 / 1080, right: 150 / 1080 }),
  '16:9': Object.freeze({ top: 0, bottom: 0, left: 0, right: 0 }),
  '1:1': Object.freeze({ top: 0, bottom: 0, left: 0, right: 0 }),
});

const KEYS = new Set(['format', 'title', 'scenario', 'ratios', 'size', 'film']);
const FILM_KEYS = new Set(['seconds', 'fps', 'casts']);

/** Every spell id the authored content declares (`data/Spells/**`), read once. */
function authoredSpells(folder) {
  const ids = new Set();
  const walk = path => {
    for (const entry of readdirSync(path, { withFileTypes: true })) {
      const inner = join(path, entry.name);
      if (entry.isDirectory()) walk(inner);
      else if (entry.name.endsWith('.json')) {
        const id = JSON.parse(readFileSync(inner, 'utf8')).id;
        if (typeof id === 'string') ids.add(id);
      }
    }
  };
  walk(folder);
  return ids;
}

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
export function readShot(path, { scenarios = join(root, 'scenarios'), spells = join(root, 'data/Spells') } = {}) {
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
  checkScenario(shot.scenario, refuse, scenarios);
  const ratios = shot.ratios ?? Object.keys(RATIOS);
  if (!Array.isArray(ratios) || ratios.length === 0) refuse(`"ratios" lists one or more of ${Object.keys(RATIOS).join(', ')}.`);
  for (const ratio of ratios) if (!Object.hasOwn(RATIOS, ratio)) refuse(`the ratio ${JSON.stringify(ratio)} is not one of ${Object.keys(RATIOS).join(', ')}.`);
  if (new Set(ratios).size !== ratios.length) refuse('"ratios" names a ratio twice.');
  const size = shot.size ?? 1080;
  if (!SIZES.includes(size)) refuse(`"size" is ${SIZES.join(' or ')}.`);
  const film = shot.film === undefined ? null : readFilm(shot.film, refuse, spells);
  return { name, title: shot.title, scenario: shot.scenario, id: scenarioId(shot.scenario), ratios, size, film };
}

// A shot's scenario: a file directly under `scenarios/` that the host lists. A tutorial or a bug scenario opens with
// its lesson or its report: a shot plays a scenario of its own.
function checkScenario(scenario, refuse, scenarios) {
  if (typeof scenario !== 'string' || !/^[a-z0-9][a-z0-9-]*\.json$/.test(scenario)) {
    refuse('"scenario" names a file directly under scenarios/, such as "greedy-1v1-round-3.json".');
  }
  const named = basename(scenario, '.json');
  const prefixes = LEFT_OUT.map(one => JSON.stringify(one + '...')).join(' or ');
  if (LEFT_OUT.some(prefix => named.startsWith(prefix))) refuse(`the host leaves out a file directly under scenarios/ named ${prefixes}.`);
  if (named.length > LONGEST_NAME) refuse(`the host cuts a scenario's name past ${LONGEST_NAME} characters: name the file shorter.`);
  if (!existsSync(join(scenarios, scenario))) refuse(`the scenario scenarios/${scenario} does not exist.`);
}

// A shot's film: how long, at what rate, and the spells the filmed seat casts when it can.
function readFilm(film, refuse, spells) {
  if (film === null || typeof film !== 'object' || Array.isArray(film)) refuse('"film" is an object: its "seconds", "fps" and "casts".');
  const unknown = Object.keys(film).filter(key => !FILM_KEYS.has(key));
  if (unknown.length) refuse(`unknown key ${unknown.map(key => JSON.stringify(key)).join(', ')} in "film".`);
  if (!Number.isInteger(film.seconds) || film.seconds < 1 || film.seconds > LONGEST_FILM) refuse(`"film.seconds" is a whole number from 1 to ${LONGEST_FILM}.`);
  const fps = film.fps ?? 30;
  if (!FRAME_RATES.includes(fps)) refuse(`"film.fps" is ${FRAME_RATES.join(' or ')}.`);
  const casts = film.casts ?? [];
  if (!Array.isArray(casts) || casts.some(spell => typeof spell !== 'string')) refuse('"film.casts" lists spell ids, such as "spell:pummel:v1".');
  const known = casts.length ? authoredSpells(spells) : new Set();
  for (const spell of casts) if (!known.has(spell)) refuse(`"film.casts" names ${JSON.stringify(spell)}, which no spell of data/Spells/ declares.`);
  return { seconds: film.seconds, fps, casts };
}

/**
 * What the filmed seat answers to the question its view asks, from the options the host offered: no mutation
 * bought, a Standard tempo, ties as rolled, and the first spell of the shot's `casts` it can cast, else the first it
 * can, on the first targets offered. Nothing is computed that the host did not offer. Null when nothing is asked.
 */
export function answer(view, casts = []) {
  const options = view?.options ?? {};
  const asked = view?.waitingAsked;
  const pick = spells => casts.find(spell => spells.includes(spell)) ?? spells[0];
  const aim = legal => legal.candidates.slice(0, Math.min(Math.max(legal.minTargets, 1), legal.maxTargets, legal.candidates.length));
  switch (options.kind) {
    case 'Evolution': return { asked, kind: 'Evolution', pass: true };
    case 'Speed': return { asked, kind: 'Speed', creature: view.waitingCreature ?? options.speed.missing[0], speed: 'Standard' };
    case 'TieOrder': return { asked, kind: 'TieOrder', order: options.tieOrder.ties.flat() };
    case 'Intent': {
      const creature = options.intent.creatures.find(one => one.creature === view.waitingCreature) ?? options.intent.creatures[0];
      return { asked, kind: 'Intent', creature: creature.creature, spell: pick(creature.castableSpells) };
    }
    case 'Target': return { asked, kind: 'Target', targets: aim(options.target.legalTargets) };
    case 'Action': {
      const castable = options.action.spells.filter(one => one.legalTargets.isCastable);
      if (!castable.length) throw new Error('The filmed seat is asked to act and can cast nothing.');
      const spell = castable.find(one => one.spell === pick(castable.map(each => each.spell)));
      return { asked, kind: 'Action', creature: options.action.actor, spell: spell.spell, targets: aim(spell.legalTargets) };
    }
    default: return null;
  }
}

/** The page's clock, in whole milliseconds, at the end of each frame of a film: frame `index` ends at `at(index)`. */
export const frameEnd = (index, fps) => Math.round(((index + 1) * 1000) / fps);

/** The page's screen and density for a ratio at a size: the picture is the screen times the density. */
export function framing(ratio, size) {
  const screen = RATIOS[ratio];
  return { viewport: { ...screen }, density: size / Math.min(screen.width, screen.height) };
}

/** A still's file: `still-9x16.png`. */
export const stillName = ratio => `still-${ratio.replace(':', 'x')}.png`;

/** A film's files, by ratio: its frames' folder, the MP4 and the sheet. */
export const filmNames = ratio => {
  const tag = ratio.replace(':', 'x');
  return { frames: `frames-${tag}`, film: `film-${tag}.mp4`, sheet: `sheet-${tag}.png` };
};

export function parseArguments(argv) {
  const options = { shot: null, still: false, film: false, ratio: null, out: null };
  for (let index = 0; index < argv.length; index++) {
    const word = argv[index];
    if (word === '--still') options.still = true;
    else if (word === '--film') options.film = true;
    else if (word === '--ratio' || word === '--out') {
      if (index + 1 >= argv.length) throw new Error(`${word} needs a value.`);
      options[word.slice(2)] = argv[++index];
    } else if (word.startsWith('--')) throw new Error(`Unknown option ${word}.`);
    else if (options.shot) throw new Error('Name one shot.');
    else options.shot = word;
  }
  if (!options.shot) throw new Error('Usage: node scripts/promo/shoot.mjs promo/shots/<shot>.json --still|--film [--ratio 9:16] [--out <folder>]');
  if (options.still === options.film) throw new Error('Name --still (one frame at each ratio) or --film (the shot\'s film), not both.');
  if (options.ratio !== null && !Object.hasOwn(RATIOS, options.ratio)) throw new Error(`--ratio is one of ${Object.keys(RATIOS).join(', ')}.`);
  return options;
}

// Math.random, seeded: the page draws the same scatter each time. A linear congruential generator is enough for
// pictures; nothing here is a secret.
function pinRandom() {
  let state = 7;
  Math.random = () => { state = (Math.imul(state, 1664525) + 1013904223) >>> 0; return state / 4294967296; };
}

// What the page is still loading on the wall's clock, and the browser's own frames. A picture decodes on the wall's
// clock, and the battle view's renderer fetches a texture and then decodes it, so a frame waits until none is left,
// or a take would show a creature's new art one frame early or late: counted at the requests the page sends, until
// their handlers have run, and at the `src` a script sets on a picture outside the page, which is how a texture
// is decoded. The field is laid out again when a sheet docks beside it, by an observer the browser calls at its own
// frames, not the page's clock: `__promoRealFrame` waits for one of those. Registered before the clock is held.
function countLoading() {
  const loading = new Set();
  let requests = 0;
  const send = XMLHttpRequest.prototype.send;
  XMLHttpRequest.prototype.send = function (...rest) {
    requests++;
    this.addEventListener('loadend', () => { requests--; }, { once: true });
    return send.apply(this, rest);
  };
  const source = Object.getOwnPropertyDescriptor(HTMLImageElement.prototype, 'src');
  Object.defineProperty(HTMLImageElement.prototype, 'src', {
    configurable: true,
    get() { return source.get.call(this); },
    set(value) {
      loading.add(this);
      const done = () => loading.delete(this);
      this.addEventListener('load', done, { once: true });
      this.addEventListener('error', done, { once: true });
      source.set.call(this, value);
    },
  });
  // A picture in the page is counted by its own state, but for one loaded lazily: it loads once it is scrolled to,
  // which a window's painting out of sight never is.
  Object.defineProperty(window, '__promoLoading', {
    value: () => requests + [...loading].filter(image => !image.isConnected).length
      + [...document.images].filter(image => image.src && !image.complete && image.loading !== 'lazy').length,
  });
  const frame = window.requestAnimationFrame.bind(window);
  Object.defineProperty(window, '__promoRealFrame', { value: () => new Promise(done => frame(() => frame(done))) });
}

// The moment the page's clock starts from: fixed, so whatever the page dates is dated the same.
const EPOCH = new Date('2026-01-01T00:00:00Z');
const TICK_MS = 100;

/**
 * Run `step` until `done` says so, at most `times` times, one after the other: each step acts on what the last one
 * left. `done` is asked before each step and once after the last; it is the answer.
 */
export async function steps(step, { times, done = () => false }) {
  // `done` may answer at once or from the page: either way, a promise.
  if (await Promise.resolve(done())) return true;
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

// The routes a page hears its table's changes on (ADR 0164): the seat's stream and the lobby's.
const STREAM = /\/api\/(?:seat\/[^/]+|lobby)\/events$/;
const SEAT_READ = /^\/api\/seat\/[^/]+$/;

/**
 * Hold the page's streams, so it hears of a change only when the film says so. A stream says "read again" at the
 * moment the host changes, by the wall's clock, and a host that answers a decision and then the scripted
 * opponent's changes twice: a page that read in between would draw a step a take later or not at all. The seat's
 * stream is held until `tell()` answers it with one change, which ends it; the page opens it again on its own
 * clock. The lobby's is never answered: nothing it lists is filmed. The first seat stream is answered at once, as
 * the host's own says it is ready.
 */
async function holdStreams(page, release) {
  let held = null, owed = true, read = null;
  // A stream answered is a read the page owes: the next frame waits for it, whenever the answer reaches the page.
  const changed = route => {
    read = page.waitForRequest(request => SEAT_READ.test(new URL(request.url()).pathname) && request.method() === 'GET', { timeout: 30000 });
    read.catch(() => {});
    return route.fulfill({ status: 200, contentType: 'text/event-stream', body: 'event: changed\ndata: \n\n' }).catch(() => {});
  };
  await page.route(STREAM, route => {
    if (!route.request().url().includes('/api/seat/')) return;
    if (owed) { owed = false; void changed(route); } else { held = route; release(route.request()); }
  });
  return {
    // Say "read again" now, or as soon as the page opens its stream again.
    tell: () => { if (held) { const route = held; held = null; return changed(route); } owed = true; return Promise.resolve(); },
    // The read the page owes for the last change it was told of, once it has started.
    owed: async () => { const pending = read; read = null; if (pending) await pending; },
  };
}

/**
 * Open the shot's scenario on the host at `origin` in a new context framed at `ratio`, as a player opens it, and
 * hand `work` the page with its clock: `tick(times)` moves it a tick of 100 ms at a time, `run(ms)` by `ms`, each
 * once what the page asked for has come. Give it a host of its own: an account that opened the scenario before
 * would resume its practice.
 */
async function onScenario(browser, origin, shot, ratio, work, { hold = false } = {}) {
  const { viewport, density } = framing(ratio, shot.size);
  // Less motion: the field at rest, as a player who asks for it sees it. Its embers and its breath follow the
  // time since the view arrived, which no pinned clock holds the same from one take to the next.
  const context = await browser.newContext({ viewport, deviceScaleFactor: density, reducedMotion: 'reduce', locale: 'en-US', timezoneId: 'UTC' });
  try {
    const page = await context.newPage();
    // What the page is still waiting for, but the event streams, which stay open for as long as the page does.
    const open = new Set(), waiting = [];
    // The seat's key, and the address of the effects kit once the page asks for it.
    let token = null, kit = null, kitReady = false;
    // A held seat stream is waited for until it is held or answered; the lobby's, and a stream not held, never.
    const stays = path => (hold ? /\/api\/lobby\/events$/ : STREAM).test(path);
    page.on('request', request => {
      token ??= request.headers()['x-seat-token'] ?? null;
      if (new URL(request.url()).pathname.endsWith('/fx.js')) kit ??= request.url();
      if (request.resourceType() !== 'eventsource' && !stays(new URL(request.url()).pathname)) open.add(request);
    });
    const done = request => {
      open.delete(request);
      if (open.size === 0) for (const answered of waiting.splice(0)) answered();
    };
    page.on('requestfinished', done);
    page.on('requestfailed', done);
    // Every request the page has made is answered: a request that never is fails the shot, not its bytes.
    const settled = () => open.size === 0 ? Promise.resolve() : new Promise((answered, fail) => {
      const late = setTimeout(() => fail(new Error(`The page still waits for ${[...open].map(one => one.url()).join(', ')}.`)), 30000);
      waiting.push(() => { clearTimeout(late); answered(); });
    });
    const streams = hold ? await holdStreams(page, done) : null;
    // A request the page has made may not have reached this process yet: one round trip to the page first. A held
    // stream answered owes a read, which is waited for in turn.
    const incomplete = () => page.evaluate(() => [...document.images].filter(image => image.src && !image.complete)
      .map(image => image.loading + ' ' + image.src.slice(0, 80)).join(', '));
    // Asked again every 20 ms, one ask after the other, for 30 s at most.
    const loaded = (end = Date.now() + 30000) => page.evaluate(() => window.__promoLoading?.() ?? 0).then(async left => {
      if (left === 0) return;
      if (Date.now() > end) throw new Error(`The page was still loading after 30 s; pictures not complete: ${await incomplete()}`);
      await new Promise(wait => setTimeout(wait, 20));
      return loaded(end);
    });
    // The effects kit (battle/src/fx-loader.ts) is a module the page imports once its first frame is drawn: fetched,
    // it is still to be run, and a spell is drawn as its motif until it has. Importing it here resolves once it ran.
    const kitRan = async () => {
      if (!kit || kitReady) return;
      await page.evaluate(url => import(url).then(() => true, () => false), kit);
      kitReady = true;
    };
    const flushed = () => page.evaluate(() => 0).then(settled).then(loaded).then(kitRan).then(() => page.evaluate(() => window.__promoRealFrame?.()));
    const caughtUp = () => flushed().then(() => streams?.owed()).then(flushed);
    // The page's clock moves only here, once what the last move asked for has come.
    const run = ms => page.clock.runFor(ms).then(caughtUp);
    const tick = (times = 1) => steps(() => run(TICK_MS), { times });
    await page.addInitScript(pinRandom);
    await page.addInitScript(countLoading);
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
    if (await page.locator('#practice-band').count() !== 1) throw new Error('The practice\'s band (#practice-band) is not on the page: the shot would show what it hides.');
    await page.addStyleTag({ content: '#practice-band { display: none !important; }' });
    await tick(30);
    return await work({ page, token: () => token, run, caughtUp, streams });
  } finally {
    await context.close();
  }
}

/** Open the shot's scenario framed at `ratio` and return the PNG of the screen (`onScenario`). */
export async function shootStill(browser, origin, shot, ratio) {
  return onScenario(browser, origin, shot, ratio, ({ page }) => page.screenshot());
}

// How long the filmed seat looks at what the page shows before it acts, in the film's own time: long enough for a
// viewer to read it.
const BEAT_MS = 800;

// The buttons a reading waits on a player for (table/battle.html): OK after an opponent's action, Continue after the
// turn order. The filmed seat presses them as a player does.
const PACED = ['#playback-next', '#order-continue'];

/**
 * What the page shows a player could act on now: a window open over the field (a phase's tip opens the first time a
 * phase comes), the paced button on screen, and whether a result is being read.
 */
const pageState = page => page.evaluate(paced => ({
  window: document.querySelector('dialog[open]') ? document.querySelector('dialog[open]').id || '(a window)' : null,
  reading: document.querySelector('#battle')?.dataset.reading === 'true',
  button: paced.find(selector => { const node = document.querySelector(selector); return node && !node.hidden && node.offsetParent !== null; }) ?? null,
}), PACED);

/**
 * What the filmed seat would act on now, as one name, or null: a window open over the field first, then the button
 * a reading waits on, then the host's question once nothing is being read. The seat acts once the same name has
 * been on screen for a beat.
 */
export function actOn(state, asking) {
  if (state.window) return `window ${state.window}`;
  if (state.button) return state.button;
  return !state.reading && !asking.over && asking.waitingFor ? `asked ${asking.waitingAsked}` : null;
}

/**
 * Film the shot framed at `ratio`: open its scenario as a still does, then step the page's clock one frame of the
 * film at a time and hand `frame(index, png)` each, for `film.seconds`. The seat plays as a player does, `BEAT_MS`
 * after what it acts on is on screen: it closes a window opened over the field, presses OK or Continue when a
 * reading waits for it, and answers the host's
 * question (`answer`) through the seat's own route once nothing is being read. The scripted opponent answers on the
 * host. After each answer the host is let finish, and only then is the page told to read again, so the page draws
 * the same steps at the same frames each take. Returns the decisions the seat sent, in order.
 */
export async function shootFilm(browser, origin, shot, ratio, frame) {
  if (!shot.film) throw new Error(`The shot ${shot.name} has no "film".`);
  const { seconds, fps, casts } = shot.film;
  return onScenario(browser, origin, shot, ratio, async ({ page, token, run, caughtUp, streams }) => {
    const seat = async () => {
      const answered = await page.request.get(`${origin}/api/seat/player1`, { headers: { 'X-Seat-Token': token() } });
      if (!answered.ok()) throw new Error(`The host refused the seat's view: ${answered.status()}.`);
      return answered.json();
    };
    // The host has finished with a decision once the seat is asked something else, or the match is over.
    const settles = async before => {
      const end = Date.now() + 30000;
      for (;;) {
        const view = await seat();
        if (view.over || (view.waitingFor && view.waitingAsked !== before)) return view;
        if (Date.now() > end) throw new Error(`The host did not move on from asking ${before}.`);
        await new Promise(wait => setTimeout(wait, 50));
      }
    };
    // What the host asks the seat, read once at the start and again after each answer: it changes only then.
    let asking = await seat();
    const decisions = [];
    const act = async state => {
      if (state.window) return page.keyboard.press('Escape');
      // Pressed in the page, in one go: Playwright's click waits on the browser's own frames for the button to stand
      // still, and the page's clock would not be where it was each take.
      if (state.button) return page.evaluate(selector => document.querySelector(selector).click(), state.button);
      const decision = answer(asking, casts);
      const sent = await page.request.post(`${origin}/api/seat/player1/decision`, { headers: { 'X-Seat-Token': token() }, data: decision });
      if (sent.status() !== 204) throw new Error(`The host refused ${JSON.stringify(decision)}: ${sent.status()} ${await sent.text()}`);
      decisions.push(decision);
      asking = await settles(decision.asked);
      // The page reads at once on a stream it holds open, or once its clock opens the stream again.
      return streams.tell();
    };
    // What the seat would act on, and from when: it acts once the same thing has been on screen for a beat. The frames
    // are taken one after the other, each from what the last one left: the awaits in this loop are the film.
    let seen = null, since = 0;
    const frames = seconds * fps;
    for (let index = 0; index < frames; index++) {
      const now = Math.round((index * 1000) / fps);
      const state = await pageState(page);
      const next = actOn(state, asking);
      if (next !== seen) { seen = next; since = now; }
      if (seen && now - since >= BEAT_MS) {
        await act(state);
        await caughtUp();
        seen = null;
      }
      await run(frameEnd(index, fps) - now);
      // CSS transitions and animations run on the compositor's clock, which the page's does not hold: each frame
      // shows them finished, as a held clock would after its tick. The field's motion is drawn on the page's clock.
      await frame(index, await page.screenshot({ animations: 'disabled' }));
    }
    return decisions;
  }, { hold: true });
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

export const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');

/** One hash for a film's frames, in order: two takes that draw the same frames give the same digest. */
export const framesDigest = hashes => sha256(hashes.join('\n'));

/** Which frames a sheet shows: `picks` of `count`, evenly spaced, the first and the last among them. */
export function sheetFrames(count, picks = 12) {
  if (count <= picks) return Array.from({ length: count }, (_, index) => index);
  return Array.from({ length: picks }, (_, index) => index).map(index => Math.round((index * (count - 1)) / (picks - 1)));
}

/** Whether `ffmpeg` and `ffprobe` answer: a film is encoded and measured with them. */
// NOSONAR below: javascript:S4036, ffmpeg and ffprobe are found on the PATH of whoever films a shot (a session or the
// promo workflow's runner); the arguments are the script's own and a checked shot's numbers, nothing a visitor sends.
export const hasEncoder = () => ['ffmpeg', 'ffprobe'].every(tool => spawnSync(tool, ['-version'], { stdio: 'ignore' }).status === 0); // NOSONAR

/** How a film is encoded, after its inputs: the options `encodeFilm` names, shared by a cut (`cut.mjs`). */
export const ENCODING = Object.freeze(['-c:v', 'libx264', '-preset', 'slow', '-crf', '18', '-pix_fmt', 'yuv420p', '-threads', '1',
  '-bsf:v', 'filter_units=remove_types=6', '-map_metadata', '-1', '-fflags', '+bitexact', '-flags:v', '+bitexact', '-movflags', '+faststart']);

/**
 * Encode the PNG frames of `folder` (`000000.png` on) at `fps` into an H.264 MP4 a feed takes: yuv420p, the index
 * at the front, one thread, no date, and without the note x264 writes of its version and options (an SEI unit, cut
 * by `filter_units`). One set of frames gives one file with one build of x264: another build may code the frames
 * otherwise, which the workflow records beside the hashes.
 */
export function encodeFilm(folder, fps, out) {
  const run = spawnSync('ffmpeg', ['-y', // NOSONAR: S4036, as for hasEncoder
    '-loglevel', 'error', '-framerate', String(fps), '-i', join(folder, '%06d.png'), ...ENCODING, out], { encoding: 'utf8' });
  if (run.status !== 0) throw new Error(`ffmpeg could not encode ${folder}: ${run.stderr || run.error?.message}`);
}

/** What a film is, read back with ffprobe: its width, height, frame count and length in seconds. */
export function filmFacts(path) {
  const run = spawnSync('ffprobe', ['-v', // NOSONAR: S4036, as for hasEncoder
    'error', '-select_streams', 'v:0', '-count_frames', '-show_entries',
    'stream=width,height,nb_read_frames,codec_name,pix_fmt:format=duration', '-of', 'json', path], { encoding: 'utf8' });
  if (run.status !== 0) throw new Error(`ffprobe could not read ${path}: ${run.stderr || run.error?.message}`);
  const facts = JSON.parse(run.stdout);
  const stream = facts.streams[0];
  return { width: stream.width, height: stream.height, frames: Number(stream.nb_read_frames), codec: stream.codec_name, pixels: stream.pix_fmt, seconds: Number(facts.format.duration) };
}

/**
 * The film's sheet: twelve of its frames on a grid, each with the margins a feed covers (`SAFE_ZONES`) hatched
 * over it and its time under it, drawn by the browser into one PNG.
 */
export async function drawSheet(browser, pngs, ratio, fps) {
  const zone = SAFE_ZONES[ratio];
  const picked = sheetFrames(pngs.length);
  const { width, height } = RATIOS[ratio];
  const columns = width < height ? 6 : 3;
  const cell = index => `<figure><div class="frame"><img src="data:image/png;base64,${pngs[index].toString('base64')}" alt="">`
    + `<i style="top:0;left:0;right:0;height:${zone.top * 100}%"></i><i style="bottom:0;left:0;right:0;height:${zone.bottom * 100}%"></i>`
    + `<i style="top:0;bottom:0;left:0;width:${zone.left * 100}%"></i><i style="top:0;bottom:0;right:0;width:${zone.right * 100}%"></i></div>`
    + `<figcaption>${(index / fps).toFixed(2)} s · frame ${index}</figcaption></figure>`;
  const page = await browser.newPage({ viewport: { width: columns * 200 + 40, height: 400 } });
  try {
    // The sheet is a working picture for the owner, not the brand's: plain type, the zones in a warning red.
    await page.setContent(`<!doctype html><style>
      body { margin: 0; padding: 16px; background: #222; color: #ddd; font: 12px/1.3 system-ui, sans-serif; }
      main { display: grid; grid-template-columns: repeat(${columns}, 192px); gap: 8px; }
      figure { margin: 0; } .frame { position: relative; aspect-ratio: ${width} / ${height}; }
      img { display: block; width: 100%; height: 100%; } i { position: absolute; background: repeating-linear-gradient(45deg, rgb(255 40 40 / 0.45) 0 6px, transparent 6px 12px); }
      figcaption { padding-top: 2px; }
    </style><p>${ratio} · ${pngs.length} frames at ${fps} fps · hatched: what a feed covers</p><main>${picked.map(cell).join('')}</main>`);
    await page.locator('img').evaluateAll(images => Promise.all(images.map(image => image.decode())));
    return await page.screenshot({ fullPage: true });
  } finally {
    await page.close();
  }
}

// Where a path is printed: from the repository's root when it is in it.
const shownPath = path => path.startsWith(`${root}${sep}`) ? relative(root, path) : path;

async function main(argv) {
  const options = parseArguments(argv);
  const shot = readShot(resolve(options.shot));
  if (options.ratio && !shot.ratios.includes(options.ratio)) throw new Error(`--ratio ${options.ratio} is not one of the shot's: ${shot.ratios.join(', ')}.`);
  if (options.film && !shot.film) throw new Error(`${options.shot}: the shot has no "film" to shoot.`);
  if (options.film && !hasEncoder()) throw new Error('A film needs ffmpeg and ffprobe on the PATH.');
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
    const still = async ratio => {
      const png = await onHost(startHost, origin => shootStill(browser, origin, shot, ratio));
      const path = join(folder, stillName(ratio));
      await writeFile(path, png);
      console.log(`${shownPath(path)}  sha256:${sha256(png)}`);
    };
    const film = async ratio => {
      const names = filmNames(ratio);
      const frames = join(folder, names.frames);
      await rm(frames, { recursive: true, force: true });
      await mkdir(frames, { recursive: true });
      const hashes = [];
      await onHost(startHost, origin => shootFilm(browser, origin, shot, ratio, async (index, png) => {
        hashes.push(sha256(png));
        await writeFile(join(frames, `${String(index).padStart(6, '0')}.png`), png);
      }));
      const out = join(folder, names.film);
      encodeFilm(frames, shot.film.fps, out);
      const pngs = await Promise.all(hashes.map((_, index) => readFile(join(frames, `${String(index).padStart(6, '0')}.png`))));
      await writeFile(join(folder, names.sheet), await drawSheet(browser, pngs, ratio, shot.film.fps));
      console.log(`${shownPath(out)}  sha256:${sha256(await readFile(out))}  frames:${hashes.length} digest:${framesDigest(hashes)}`);
      console.log(`${shownPath(join(folder, names.sheet))}`);
    };
    // One ratio after the other, each on a host of its own.
    const shootOne = options.film ? film : still;
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
