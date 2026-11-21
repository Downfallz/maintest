#!/usr/bin/env node
// The journey film and its measures (docs/art/ux-craft-plan.md, UX2): one journey walked in Chromium as a new
// player would, on the built showcase and a real table host with developer accounts, at 320 by 568 and 390 by
// 844. For each width it writes a contact sheet, the screens in order, numbered and named, and a file of
// measures, a line a screen. Two walks of one journey give the same measures.
//
//   node scripts/ux/journey.mjs doors                         # artifacts/journeys/doors-390.png, doors-390.measures.jsonl, and -320
//   node scripts/ux/journey.mjs --all --out <folder>          # every journey
//   node scripts/ux/journey.mjs lobby --width 390             # one width
//   node scripts/ux/journey.mjs lobby --strip create-window   # one interaction across time: lobby-create-window-390.png
//   node scripts/ux/journey.mjs --paint                       # the first paints on a slow phone: paint.json
//   node scripts/ux/journey.mjs --list                        # the journeys, their screens and their strips
//
// It needs the engine built, the content consolidated, the battle bundle and the browser suite's Playwright.

import { createRequire } from 'node:module';
import { mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { JOURNEYS } from './journeys.mjs';
import { AXE_TAGS, TARGET, TARGET_MINIMUM, TEXT_FLOOR, axeInPage, measureInPage, measuresLines, watchShiftInPage } from './measures.mjs';
import { PAINT_RUNS, paints } from './paint.mjs';
import { installSeatViews } from '../../studio/browser/seat-views.js';
import { buildShowcase, missing, raiseSet, root } from './set.mjs';

export const VIEWPORTS = [{ width: 320, height: 568 }, { width: 390, height: 844 }];
const COLUMNS = 5;
/** A page taller than this many screens is cut on its sheet, and its caption says how tall it is. */
const SCREENS_SHOWN = 3;
const STOP = Symbol('the strip is filmed');

// The browser suite's pinned Playwright when it is installed, or the machine's own; and its axe, when it has one.
const sources = [join(root, 'studio/browser/package.json'), import.meta.url];
function playwright() {
  for (const from of sources) {
    try { return createRequire(from)('playwright'); } catch { /* try the next one */ }
  }
  throw new Error('Playwright is not installed: run `npm --prefix studio/browser ci --ignore-scripts`.');
}
export async function axeSource() {
  for (const from of sources) {
    try { return await readFile(createRequire(from).resolve('axe-core/axe.min.js'), 'utf8'); } catch { /* try the next one */ }
  }
  return null;
}

/** Wait until the screen measures the same twice: its fonts, its pictures and what the host sent are in. */
async function settle(page) {
  await page.evaluate(() => document.fonts.ready.then(() => Promise.all([...document.images].filter(image => image.loading !== 'lazy' && !image.complete)
    .map(image => new Promise(done => { image.addEventListener('load', done, { once: true }); image.addEventListener('error', done, { once: true }); })))));
  const read = () => page.evaluate(measureInPage, { floor: TEXT_FLOOR, target: TARGET, minimum: TARGET_MINIMUM });
  let last = JSON.stringify(await read());
  for (let calm = 0, tries = 0; calm < 2 && tries < 40; tries++) {
    await page.waitForTimeout(250);
    const now = JSON.stringify(await read());
    calm = now === last ? calm + 1 : 0;
    last = now;
  }
  return JSON.parse(last);
}

async function picture(page, measures, viewport, asSeen = false) {
  // A screen the walk names `asSeen` (a section of a long page it scrolled to) is pictured as the visitor sees it.
  if (measures.window || measures.height <= viewport.height || asSeen) return page.screenshot();
  return page.screenshot({ fullPage: true, clip: { x: 0, y: 0, width: viewport.width, height: Math.min(measures.height, SCREENS_SHOWN * viewport.height) } });
}

/**
 * One interaction across time. Animations are stopped and every timer set from here on is held, then the
 * interaction is made and the page is stepped `interval` ms at a time: two films give the same frames.
 */
async function film(page, act, { frames = 12, interval = 40 } = {}) {
  const session = await page.context().newCDPSession(page);
  await session.send('Animation.enable');
  await session.send('Animation.setPlaybackRate', { playbackRate: 0 });
  await page.clock.install();
  await page.clock.pauseAt(new Date(Date.now() + 1000));
  const step = ms => page.evaluate(at => {
    const born = (globalThis.__journeyBorn ??= new WeakMap());
    const all = document.getAnimations();
    for (const one of all) {
      if (!born.has(one)) born.set(one, at);
      one.currentTime = at - born.get(one);
    }
    return all.filter(one => one.playState !== 'finished').length;
  }, ms);
  const shots = [];
  await step(0);
  shots.push({ ms: 0, animating: 0, png: await page.screenshot() });
  await act();
  for (let frame = 1; frame < frames; frame++) {
    const ms = frame * interval;
    await page.clock.runFor(interval);
    const animating = await step(ms);
    shots.push({ ms, animating, png: await page.screenshot() });
  }
  return shots;
}

/**
 * Walk one journey at one size on a browser. Returns its screens in the order reached, each { name, staged,
 * measures, png }, with `blocked`, the choices it had to make by script; or, with `strip`, the frames of that
 * interaction. It throws when the walk misses a screen it
 * names or reaches one it does not. `scale` is the screen's density: 1 for a sheet, 2 for a picture a page shows.
 */
export async function walkJourney(browser, journey, viewport, { showcase, axe = null, strip = null, frames, interval, scale = 1 } = {}) {
  const filmed = strip ? journey.strips?.[strip] : null;
  if (strip && !filmed) throw new Error(`The ${journey.id} journey has no strip named ${strip}: ${Object.keys(journey.strips ?? {}).join(', ') || 'none'}.`);
  const set = await raiseSet(showcase);
  const contexts = [];
  const person = async () => {
    // UX_JOURNEY_VIDEO=<folder> records each person's walk in real time, a clip of a stage's interaction (UX7-UX12).
    const video = process.env.UX_JOURNEY_VIDEO ? { recordVideo: { dir: join(process.env.UX_JOURNEY_VIDEO, String(viewport.width)), size: viewport } } : {};
    const context = await browser.newContext({ viewport, isMobile: true, hasTouch: true, deviceScaleFactor: scale, ...video });
    await context.addInitScript(watchShiftInPage);
    // The seat's views as the page reads them, for the walks that play (studio/browser/seat-views.js).
    await context.addInitScript(`(${installSeatViews})(window, ${JSON.stringify({ seats: ['player1', 'player2'] })});`);
    contexts.push(context);
    return context.newPage();
  };
  const screens = [], blocks = [];
  let shots = null;
  try {
    const page = await person();
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    const walk = {
      page, set, viewport, person,
      seen: name => screens.some(screen => screen.name === name),
      // A choice the page left out of a player's reach: said once, with the screen it follows.
      blocked: async what => { const line = `after ${screens.at(-1)?.name ?? 'the start'}: ${what} could not be pressed`; if (!blocks.includes(line)) blocks.push(line); },
      // The screen a player is on now: settled, measured, pictured. `staged` says the walk opened it itself;
      // `asSeen` pictures the viewport where the page is scrolled, not the page from its top.
      async screen(name, { staged = false, on = page, asSeen = false } = {}) {
        if (!journey.screens.includes(name)) throw new Error(`The ${journey.id} journey reached "${name}", which it does not name.`);
        if (walk.seen(name)) throw new Error(`The ${journey.id} journey reached "${name}" twice.`);
        const measures = await settle(on);
        if (filmed?.at === name) {
          shots = await film(on, () => filmed.act({ ...walk, page: on }), { frames, interval });
          throw STOP;
        }
        if (axe) {
          await on.evaluate(axe);
          measures.axe = await on.evaluate(axeInPage, AXE_TAGS);
        }
        screens.push({ name, staged, measures, png: await picture(on, measures, viewport, asSeen) });
      },
    };
    try { await journey.walk(walk); } catch (error) {
      if (error !== STOP && process.env.UX_JOURNEY_DEBUG) await page.screenshot({ path: process.env.UX_JOURNEY_DEBUG }).catch(() => {});
      if (error !== STOP) throw new Error(`The ${journey.id} journey stopped after "${screens.at(-1)?.name ?? 'nothing'}" at ${viewport.width} px: ${error.message}`, { cause: error });
    }
    if (strip) {
      if (!shots) throw new Error(`The ${journey.id} journey never reached "${filmed.at}", where ${strip} is filmed.`);
      return shots;
    }
    const missed = journey.screens.filter(name => !walk.seen(name));
    if (missed.length) throw new Error(`The ${journey.id} journey never reached: ${missed.join(', ')}.`);
    if (errors.length) throw new Error(`The ${journey.id} journey's page threw: ${errors.join(' | ')}`);
    // What the walk could not press rides on the screens: a walk with none is one a player can make.
    return Object.assign(screens, { blocked: blocks });
  } finally {
    for (const context of contexts) await context.close().catch(() => {});
    await set.close();
  }
}

const escape = text => String(text).replace(/[&<>"]/g, mark => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[mark]);

/** Lay pictures on one sheet, a caption under each, and return it as a PNG. */
export async function contactSheet(browser, cells, { title, columns = COLUMNS, width }) {
  const context = await browser.newContext({ deviceScaleFactor: 1 });
  const page = await context.newPage();
  try {
    const figures = cells.map(({ png, caption, note }) =>
      `<figure><img src="data:image/png;base64,${png.toString('base64')}"><figcaption><b>${escape(caption)}</b>${note ? ` ${escape(note)}` : ''}</figcaption></figure>`).join('');
    // The sheet's colours are the brand's (docs/art/branding.css), written out: the sheet loads no stylesheet.
    await page.setContent(`<!doctype html><meta charset="utf-8"><style>
      body { margin: 0; padding: 12px; background: #0b1519; color: #ece9dc; font: 13px system-ui, sans-serif; width: max-content; }
      h1 { font-size: 15px; font-weight: 600; margin: 0 0 10px; }
      main { display: grid; grid-template-columns: repeat(${columns}, ${width}px); gap: 10px; align-items: start; }
      figure { margin: 0; } img { display: block; width: ${width}px; outline: 1px solid #2a3a41; }
      figcaption { padding: 4px 2px 0; color: #adb8bd; overflow-wrap: anywhere; } b { color: #e7c183; }
    </style><h1>${escape(title)}</h1><main>${figures}</main>`);
    return await page.locator('body').screenshot();
  } finally {
    await context.close();
  }
}

const journeySheet = (browser, journey, viewport, screens) => contactSheet(browser, screens.map((screen, index) => ({
  png: screen.png,
  caption: `${index + 1}. ${screen.name}`,
  note: [screen.staged ? 'staged' : null, !screen.measures.window && screen.measures.height > SCREENS_SHOWN * viewport.height ? `${screen.measures.height} px tall, cut` : null].filter(Boolean).join(', '),
})), { title: `${journey.title} · ${viewport.width} × ${viewport.height}`, width: viewport.width });

const SWITCHES = { '--all': 'all', '--list': 'list', '--paint': 'paint' };
const VALUED = {
  '--out': (text, options) => { options.out = text; },
  '--strip': (text, options) => { options.strip = text; },
  '--showcase': (text, options) => { options.showcase = text; },
  '--width': (text, options) => {
    const viewport = VIEWPORTS.find(one => String(one.width) === text);
    if (!viewport) throw new Error(`--width is ${VIEWPORTS.map(one => one.width).join(' or ')}.`);
    options.viewports = [viewport];
  },
};
for (const flag of ['--frames', '--interval', '--runs']) {
  VALUED[flag] = (text, options) => {
    const number = Number(text);
    if (!Number.isInteger(number) || number <= 0) throw new Error(`${flag} needs a positive whole number.`);
    options[flag.slice(2)] = number;
  };
}

export function parseArguments(argv) {
  const options = { journeys: [], all: false, list: false, paint: false, out: null, strip: null, showcase: null, viewports: VIEWPORTS, runs: PAINT_RUNS };
  for (let index = 0; index < argv.length; index++) {
    const word = argv[index];
    if (Object.hasOwn(SWITCHES, word)) options[SWITCHES[word]] = true;
    else if (Object.hasOwn(VALUED, word)) {
      index += 1;
      if (index >= argv.length) throw new Error(`${word} needs a value.`);
      VALUED[word](argv[index], options);
    } else if (word.startsWith('--')) throw new Error(`Unknown option ${word}.`);
    else if (JOURNEYS.some(journey => journey.id === word)) options.journeys.push(word);
    else throw new Error(`No journey is named ${word}: ${JOURNEYS.map(journey => journey.id).join(', ')}.`);
  }
  if (options.list) return options;
  if (options.all && options.journeys.length) throw new Error('Name journeys, or --all, not both.');
  if (!options.all && !options.journeys.length && !options.paint) throw new Error(`Name a journey (${JOURNEYS.map(journey => journey.id).join(', ')}), --all, --paint or --list.`);
  if (options.strip) {
    if (options.all || options.journeys.length !== 1) throw new Error('--strip films one interaction of one journey.');
    const journey = JOURNEYS.find(one => one.id === options.journeys[0]);
    if (!journey.strips?.[options.strip]) throw new Error(`The ${journey.id} journey has no strip named ${options.strip}: ${Object.keys(journey.strips ?? {}).join(', ') || 'none'}.`);
  }
  return options;
}

/** The journeys as `--list` prints them. */
export function listing() {
  return JOURNEYS.map(journey => [`${journey.id}: ${journey.title}`, `  screens: ${journey.screens.join(', ')}`,
    `  strips: ${Object.keys(journey.strips ?? {}).join(', ') || 'none'}`].join('\n')).join('\n');
}

async function main(argv) {
  const options = parseArguments(argv);
  if (options.list) { console.log(listing()); return; }
  const lacking = missing();
  if (lacking.length) throw new Error(lacking.join('\n'));
  const { chromium } = playwright();
  const out = resolve(options.out ?? join(root, 'artifacts/journeys'));
  await mkdir(out, { recursive: true });
  const axe = await axeSource();
  if (!axe) console.error('axe-core is not installed (`npm --prefix studio/browser ci --ignore-scripts`): the measures say axe was not run.');
  const built = options.showcase ? null : await mkdtemp(join(tmpdir(), 'ux-showcase-'));
  let browser;
  try {
    const showcase = options.showcase ? resolve(options.showcase) : await buildShowcase(join(built, 'dist'));
    browser = await chromium.launch({ executablePath: process.env.STUDIO_CHROMIUM || undefined });
    const journeys = options.all ? JOURNEYS : JOURNEYS.filter(journey => options.journeys.includes(journey.id));
    // One walk after the other, on purpose: each raises a host, and two at once would share the machine's time.
    for (const journey of journeys) {
      for (const viewport of options.viewports) {
        if (options.strip) {
          const shots = await walkJourney(browser, journey, viewport, { showcase, strip: options.strip, frames: options.frames, interval: options.interval });
          const path = join(out, `${journey.id}-${options.strip}-${viewport.width}.png`);
          await writeFile(path, await contactSheet(browser, shots.map(shot => ({ png: shot.png, caption: `${shot.ms} ms`, note: `${shot.animating} animating` })),
            { title: `${journey.title} · ${options.strip} · ${viewport.width} × ${viewport.height}`, columns: 6, width: viewport.width }));
          console.log(path);
          continue;
        }
        const screens = await walkJourney(browser, journey, viewport, { showcase, axe });
        const sheet = join(out, `${journey.id}-${viewport.width}.png`), measures = join(out, `${journey.id}-${viewport.width}.measures.jsonl`);
        await writeFile(sheet, await journeySheet(browser, journey, viewport, screens));
        await writeFile(measures, measuresLines({ journey: journey.id, viewport, axe: Boolean(axe), blocked: screens.blocked, screens }));
        console.log(sheet); console.log(measures);
        for (const line of screens.blocked) console.error(`${journey.id} at ${viewport.width} px, ${line}`);
      }
    }
    if (options.paint) {
      const path = join(out, 'paint.json');
      await writeFile(path, `${JSON.stringify(await paints(browser, showcase, { runs: options.runs }), null, 2)}\n`);
      console.log(path);
    }
  } finally {
    await browser?.close();
    if (built) await rm(built, { recursive: true, force: true });
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
