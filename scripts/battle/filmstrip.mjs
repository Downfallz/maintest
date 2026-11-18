#!/usr/bin/env node
// The film strip of one effect (docs/art/battle-craft-plan.md, BC2): the effects bench (`/battle-lab?bench`)
// opened in Chromium at phone size, its clock stepped by a fixed interval, and twelve frames laid on one
// contact sheet with the millisecond under each. The bench stops the game's loop and pins `Date.now` and
// `Math.random` while it films, so two runs of one effect give the same sheet.
//
//   node scripts/battle/filmstrip.mjs spell:pummel:v1                       # artifacts/filmstrips/pummel.png
//   node scripts/battle/filmstrip.mjs spell:quill_shot:v1 --actor a2 --targets b1,b3 --speed 2 --out sheet.png
//   node scripts/battle/filmstrip.mjs --all --out artifacts/filmstrips      # one sheet an entry
//
// It serves `table/` itself; the bundle must be built first (`npm --prefix battle run build`).

import { createServer } from 'node:http';
import { createRequire } from 'node:module';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { dirname, extname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const PHONE = { width: 390, height: 844 };
const COLUMNS = 4;

/** Open the bench on a page and wait for its scene. `base` is where the laboratory's page is served. */
export async function openBench(page, base) {
  await page.goto(base.includes('?') ? base : `${base}?bench`);
  await page.locator('#lab[data-bench][data-ready="true"]').waitFor();
}

/**
 * Film one effect on an open bench: `frames` pictures of the field, `interval` milliseconds of the bench's clock
 * apart, the first at 0. Returns [{ ms, stage, png }], and leaves the bench at rest with its loop running.
 */
export async function filmFrames(page, { spell, actor, targets, speed, frames = 12, interval } = {}) {
  const started = await page.evaluate(options => window.battleBench.film.start(options), { spell, actor, targets, speed });
  const step = interval ?? started.interval;
  if (step % started.tick !== 0) throw new Error(`The interval must be a multiple of ${started.tick} ms.`);
  const field = page.locator('#battle-zone');
  const shots = [{ ms: 0, stage: started.stage, png: await field.screenshot() }];
  try {
    while (shots.length < frames) {
      const { elapsed, stage } = await page.evaluate(ms => window.battleBench.film.advance(ms), step);
      shots.push({ ms: elapsed, stage, png: await field.screenshot() });
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
  const shots = await filmFrames(page, options);
  const look = await page.evaluate(() => {
    const lab = document.getElementById('lab');
    const entry = window.battleBench.entries.find(one => one.id === lab.dataset.spell);
    return `${entry.name} · ${entry.family} · ${entry.motif} · ${entry.delivery}`;
  });
  return contactSheet(page.context(), shots, { title: `${look} · ${options.speed ?? 1}×`, columns: options.columns });
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
  const options = { spells: [], all: false, out: null, base: null, scale: 1 };
  const film = {};
  const value = (flag, index) => {
    if (index >= argv.length) throw new Error(`${flag} needs a value.`);
    return argv[index];
  };
  for (let index = 0; index < argv.length; index++) {
    const word = argv[index];
    if (word === '--all') options.all = true;
    else if (word === '--out') options.out = value(word, ++index);
    else if (word === '--url') options.base = value(word, ++index);
    else if (word === '--actor') film.actor = value(word, ++index);
    else if (word === '--targets') film.targets = value(word, ++index).split(',').filter(Boolean);
    else if (['--speed', '--frames', '--interval', '--columns', '--scale'].includes(word)) {
      const number = Number(value(word, ++index));
      if (!Number.isFinite(number) || number <= 0) throw new Error(`${word} needs a positive number.`);
      if (word === '--scale') options.scale = number; else film[word.slice(2)] = number;
    } else if (word.startsWith('--')) throw new Error(`Unknown option ${word}.`);
    else options.spells.push(word);
  }
  if (options.all === options.spells.length > 0) throw new Error('Name one or more effects (spell:pummel:v1), or --all.');
  return { ...options, film };
}

const fileName = spell => `${spell.split(':')[1] ?? spell}.png`;

async function main(argv) {
  const options = parseArguments(argv);
  const { chromium } = playwright();
  const served = options.base ? null : await serveTable();
  const browser = await chromium.launch({ executablePath: process.env.STUDIO_CHROMIUM || undefined });
  try {
    const context = await browser.newContext({ viewport: PHONE, deviceScaleFactor: options.scale, isMobile: true, hasTouch: true });
    const page = await context.newPage();
    await openBench(page, options.base ?? served.base);
    const spells = options.all ? await page.evaluate(() => window.battleBench.entries.map(one => one.id)) : options.spells;
    const single = spells.length === 1 && options.out?.endsWith('.png');
    const folder = single ? null : resolve(options.out ?? join(root, 'artifacts/filmstrips'));
    for (const spell of spells) {
      const sheet = await filmSheet(page, { ...options.film, spell });
      const path = single ? resolve(options.out) : join(folder, fileName(spell));
      await mkdir(dirname(path), { recursive: true });
      await writeFile(path, sheet);
      console.log(path);
    }
  } finally {
    await browser.close();
    await served?.close();
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main(process.argv.slice(2)).catch(error => { console.error(error.message); process.exitCode = 1; });
}
