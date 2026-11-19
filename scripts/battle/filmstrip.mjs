#!/usr/bin/env node
// The film strip of one effect (docs/art/battle-craft-plan.md, BC2): the effects bench (`/battle-lab?bench`)
// opened in Chromium at phone size, its clock stepped by a fixed interval, and twelve frames laid on one
// contact sheet with the millisecond under each. The bench stops the game's loop and pins `Date.now` and
// `Math.random` while it films, so two runs of one effect give the same sheet.
//
//   node scripts/battle/filmstrip.mjs spell:pummel:v1                       # artifacts/filmstrips/pummel.png
//   node scripts/battle/filmstrip.mjs spell:quill_shot:v1 --actor a2 --targets b1,b3 --speed 2 --out sheet.png
//   node scripts/battle/filmstrip.mjs spell:pummel:v1 --fizzle              # the spell fizzles
//   node scripts/battle/filmstrip.mjs spell:hateful_sacrifice:v1 --wound    # its caster pays in blood
//   node scripts/battle/filmstrip.mjs spell:pummel:v1 --damage 8 --defeat   # a heavy blow that defeats its target
//   node scripts/battle/filmstrip.mjs spell:pummel:v1 --motion              # and, beside the sheet, how the figures and the view stand at each frame
//   node scripts/battle/filmstrip.mjs --all --out artifacts/filmstrips      # one sheet an entry
//
// It serves `table/` itself; the bundle must be built first (`npm --prefix battle run build`).

import { createServer } from 'node:http';
import { createRequire } from 'node:module';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { dirname, extname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { FILM_TICK_MS } from '../../table/battle-bench.js';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const PHONE = { width: 390, height: 844 };
const COLUMNS = 4;

/** Open the bench on a page and wait for its scene. `base` is where the laboratory's page is served. */
export async function openBench(page, base) {
  const address = new URL(base, 'http://localhost');
  const asked = address.searchParams.has('bench') ? base : `${base}${address.search ? '&' : '?'}bench`;
  await page.goto(asked);
  await page.locator('#lab[data-bench][data-ready="true"]').waitFor();
  // The effects kit arrives after the first frame: a film that started before it would show a staged spell as
  // its motif. `missing` is an answer too: the kit's bundle was not built, and the motifs are what plays.
  await page.locator('#lab[data-kit]').waitFor();
}

/**
 * Film one effect on an open bench: `frames` pictures of the field, `interval` milliseconds of the bench's clock
 * apart, the first at 0. Returns [{ ms, stage, png }], and leaves the bench at rest with its loop running.
 */
export async function filmFrames(page, { spell, actor, targets, speed, outcome = 'played', wound = false, damage = null, defeat = false, frames = 12, interval } = {}) {
  // Refused before the film starts: a started film has stopped the bench's loop.
  if (interval !== undefined && interval % FILM_TICK_MS !== 0) throw new Error(`The interval must be a multiple of ${FILM_TICK_MS} ms.`);
  // The outcome and the wound are asked each time: a sheet never inherits the last one's.
  const started = await page.evaluate(options => window.battleBench.film.start(options), { spell, actor, targets, speed, outcome, wound, damage, defeat });
  const step = interval ?? started.interval;
  const field = page.locator('#battle-zone');
  // What a sheet cannot show (a breath of a pixel, a tremor of the view) is read from the scene at each frame.
  const motion = () => page.evaluate(() => window.battleBench.motion());
  const shots = [{ ms: 0, stage: started.stage, png: await field.screenshot(), motion: await motion() }];
  try {
    while (shots.length < frames) {
      const { elapsed, stage } = await page.evaluate(ms => window.battleBench.film.advance(ms), step);
      shots.push({ ms: elapsed, stage, png: await field.screenshot(), motion: await motion() });
    }
  } finally {
    await page.evaluate(() => window.battleBench.film.stop());
  }
  return shots;
}

const escape = text => String(text).replace(/[&<>"]/g, mark => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[mark]);

/** Lay the frames on one sheet, the millisecond and the stage under each, and return it as a PNG. */
export async function contactSheet(context, shots, { title = '', columns = COLUMNS } = {}) {
  const cells = shots.map(({ ms, stage, png }) =>
    `<figure><img src="data:image/png;base64,${png.toString('base64')}"><figcaption><b>${ms} ms</b> ${escape(stage)}</figcaption></figure>`).join('');
  const page = await context.newPage();
  try {
    await page.setContent(`<!doctype html><meta charset="utf-8"><style>
      body { margin: 0; padding: 12px; background: #0b1519; color: #ece9dc; font: 13px system-ui, sans-serif; width: max-content; }
      h1 { font-size: 15px; font-weight: 600; margin: 0 0 10px; }
      main { display: grid; grid-template-columns: repeat(${columns}, auto); gap: 8px; }
      figure { margin: 0; } img { display: block; }
      figcaption { padding: 4px 2px 0; color: #adb8bd; } b { color: #e7c183; }
    </style><h1>${escape(title)}</h1><main>${cells}</main>`);
    return await page.locator('body').screenshot();
  } finally {
    await page.close();
  }
}

/** One effect, filmed and laid out: the PNG of its contact sheet. */
export async function filmSheet(page, options = {}) {
  return (await filmRecord(page, options)).sheet;
}

/**
 * The sheet, and beside it what no sheet can show: at each frame, how every figure stands (its place from its
 * home, its lean, its scale over its own, its alpha) and what the view does (its trauma, its offset, its zoom).
 */
export async function filmRecord(page, options = {}) {
  const shots = await filmFrames(page, options);
  // The title is what the bench played, read back from it: the spell, its mutation, the outcome and the speed.
  const title = await page.evaluate(() => window.battleBench.title());
  const sheet = await contactSheet(page.context(), shots, { title, columns: options.columns });
  return { sheet, motion: { title, frames: shots.map(({ ms, stage, motion }) => ({ ms, stage, ...motion })) } };
}

const TYPES = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.webp': 'image/webp', '.svg': 'image/svg+xml', '.woff2': 'font/woff2' };

/** Serve `table/` on a free local port, as the host does for what the laboratory loads. */
async function serveTable() {
  const table = join(root, 'table');
  const server = createServer(async (request, response) => {
    const pathname = new URL(request.url, 'http://localhost').pathname;
    const file = resolve(table, `.${pathname === '/battle-lab' ? '/battle-lab.html' : pathname}`);
    if (!file.startsWith(`${table}/`)) { response.writeHead(404).end(); return; }
    try {
      const content = await readFile(file);
      response.writeHead(200, { 'content-type': TYPES[extname(file)] ?? 'application/octet-stream' }).end(content);
    } catch { response.writeHead(404).end(); }
  });
  await new Promise(done => server.listen(0, '127.0.0.1', done));
  return { base: `http://127.0.0.1:${server.address().port}/battle-lab`, close: () => new Promise(done => server.close(done)) };
}

// The browser suite's pinned Playwright when it is installed, or the machine's own.
function playwright() {
  for (const from of [join(root, 'studio/browser/package.json'), import.meta.url]) {
    try { return createRequire(from)('playwright'); } catch { /* try the next one */ }
  }
  throw new Error('Playwright is not installed: run `npm --prefix studio/browser ci --ignore-scripts`.');
}

export function parseArguments(argv) {
  const options = { spells: [], all: false, out: null, base: null, scale: 1, motion: false };
  const film = {};
  const value = (flag, index) => {
    if (index >= argv.length) throw new Error(`${flag} needs a value.`);
    return argv[index];
  };
  for (let index = 0; index < argv.length; index++) {
    const word = argv[index];
    if (word === '--all') options.all = true;
    else if (word === '--motion') options.motion = true;
    else if (word === '--out') options.out = value(word, ++index);
    else if (word === '--url') options.base = value(word, ++index);
    else if (word === '--actor') film.actor = value(word, ++index);
    else if (word === '--targets') film.targets = value(word, ++index).split(',').filter(Boolean);
    else if (word === '--fizzle') film.outcome = 'fizzled';
    else if (word === '--wound') film.wound = true;
    else if (word === '--defeat') film.defeat = true;
    else if (word === '--damage') {
      film.damage = Number(value(word, ++index));
      if (!Number.isInteger(film.damage) || film.damage < 0) throw new Error('--damage is a whole number of health points.');
    }
    else if (['--speed', '--frames', '--interval', '--columns', '--scale'].includes(word)) {
      const number = Number(value(word, ++index));
      if (!Number.isFinite(number) || number <= 0) throw new Error(`${word} needs a positive number.`);
      if (word === '--scale') options.scale = number; else film[word.slice(2)] = number;
    } else if (word.startsWith('--')) throw new Error(`Unknown option ${word}.`);
    else options.spells.push(word);
  }
  if (options.all === options.spells.length > 0) throw new Error('Name one or more effects (spell:pummel:v1), or --all.');
  // The live page draws no outcome with a fizzle (`actionCue`): a wound with one would film what never plays.
  if (film.outcome === 'fizzled' && film.wound) throw new Error('--fizzle and --wound do not go together: a fizzle is drawn with no outcome.');
  if (film.outcome === 'fizzled' && (film.defeat || film.damage !== undefined)) throw new Error('--fizzle goes with no --damage and no --defeat: a fizzle harms nobody.');
  if (film.speed !== undefined && ![0.5, 1, 2].includes(film.speed)) throw new Error('--speed is 0.5, 1 or 2, as the page offers.');
  if (film.interval !== undefined && film.interval % FILM_TICK_MS !== 0) throw new Error(`--interval is a multiple of ${FILM_TICK_MS} ms.`);
  if (options.out?.endsWith('.png') && (options.all || options.spells.length > 1)) throw new Error('--out names a folder when several effects are filmed.');
  return { ...options, film };
}

/** The motion of a film as JSON Lines, read down by a person or line by line by a script: the title, then a frame a line. */
export function motionLines({ title, frames }) {
  return `${JSON.stringify({ title })}\n${frames.map(frame => JSON.stringify(frame)).join('\n')}\n`;
}

// A fizzle's or a wound's sheet beside the spell's own, not over it.
export const fileName = (spell, { outcome, wound, damage, defeat } = {}) =>
  `${spell.split(':')[1] ?? spell}${outcome === 'fizzled' ? '-fizzled' : ''}${wound ? '-wound' : ''}${damage === undefined ? '' : `-${damage}`}${defeat ? '-defeat' : ''}.png`;

async function main(argv) {
  const options = parseArguments(argv);
  const { chromium } = playwright();
  const served = options.base ? null : await serveTable();
  // The launch is inside the cleanup: a browser that does not start must still close the server, or the
  // script prints its error and never exits.
  let browser;
  try {
    browser = await chromium.launch({ executablePath: process.env.STUDIO_CHROMIUM || undefined });
    const context = await browser.newContext({ viewport: PHONE, deviceScaleFactor: options.scale, isMobile: true, hasTouch: true });
    const page = await context.newPage();
    await openBench(page, options.base ?? served.base);
    const spells = options.all ? await page.evaluate(() => window.battleBench.entries.map(one => one.id)) : options.spells;
    const single = Boolean(options.out?.endsWith('.png'));
    const folder = single ? null : resolve(options.out ?? join(root, 'artifacts/filmstrips'));
    for (const spell of spells) {
      const { sheet, motion } = await filmRecord(page, { ...options.film, spell });
      const path = single ? resolve(options.out) : join(folder, fileName(spell, options.film));
      await mkdir(dirname(path), { recursive: true });
      await writeFile(path, sheet);
      console.log(path);
      if (options.motion) {
        const beside = path.replace(/\.png$/, '.motion.jsonl');
        await writeFile(beside, motionLines(motion));
        console.log(beside);
      }
    }
  } finally {
    await browser?.close();
    await served?.close();
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main(process.argv.slice(2)).catch(error => { console.error(error.message); process.exitCode = 1; });
}
