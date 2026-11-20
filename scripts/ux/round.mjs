#!/usr/bin/env node
// The round seen played on the showcase (docs/art/ux-craft-plan.md, UX6; docs/art/journeys/doors.md): three
// moves, each a picture of the real battle view. Mutate and Choose a tempo are the match journey's
// `mutation-tree` and `tempo-sheet` screens, walked on a table of seed 7 at a phone's size and twice its density,
// then cut to the part that shows the move. Cast is the effects bench's film of one spell, a few of its frames
// kept as a strip. They are written under docs/art/sources/pages/ as the pictures' sources; then
// `node scripts/ux/pictures.mjs` derives them at the size the showcase draws them.
//
//   node scripts/ux/round.mjs                 # docs/art/sources/pages/round-*.webp
//   node scripts/ux/round.mjs --out <folder>  # somewhere else, to look before replacing
//
// It needs what the journey needs (the engine built, the content consolidated, the battle bundle and the
// browser suite's Playwright) and python3 with Pillow.

import { spawnSync } from 'node:child_process';
import { mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { filmFrames, openBench, playwright, serveTable } from '../battle/filmstrip.mjs';
import { walkJourney } from './journey.mjs';
import { JOURNEYS } from './journeys.mjs';
import { buildShowcase, root } from './set.mjs';

/** A phone's screen, and the density the pictures are taken at: the showcase sends them at twice the width drawn. */
export const PHONE = { width: 390, height: 844 };
export const SCALE = 2;

/**
 * The part of each screen a move shows, in CSS pixels of the phone's screen: the whole width, and the height of
 * the battle's field on the bench (390 by 320), so the three pictures share one shape.
 */
export const FRAME = { width: 390, height: 320 };
export const SCREENS = [
  { name: 'round-mutate', screen: 'mutation-tree', top: 250 },
  { name: 'round-tempo', screen: 'tempo-sheet', top: 300 },
];

/** The spell the Cast strip films, and the milliseconds of the bench's clock its frames are taken at, the clock stepped by `interval`. */
export const CAST = { spell: 'spell:pummel:v1', damage: 8, interval: 120, frames: [0, 720, 840, 960, 1080, 1200] };

export const SOURCES = 'docs/art/sources/pages';

/** Where each frame of the strip is written: the declaration lists them in order. */
export const castFrame = index => `round-cast-${index + 1}`;

// Pillow cuts and writes: one job a picture, the part of the PNG kept and the WebP written, near lossless, since
// pictures.mjs encodes again from it.
const CUT = `
import json, sys
from PIL import Image
for job in json.load(sys.stdin):
    with Image.open(job["png"]) as shot:
        part = shot.convert("RGB").crop(tuple(job["box"])) if job.get("box") else shot.convert("RGB")
    part.save(job["out"], "WEBP", quality=94, method=6)
`;

function cut(jobs) {
  // NOSONAR S4036: python3 is found on the PATH of whoever makes the pictures; nothing a visitor sends reaches this.
  const run = spawnSync('python3', ['-c', CUT], { input: JSON.stringify(jobs), encoding: 'utf8' }); // NOSONAR
  if (run.status !== 0) throw new Error(`Pillow could not cut the pictures: ${run.stderr || run.error?.message}`);
}

/** The box of a screen's picture that a move keeps, in the screenshot's own pixels. */
export function boxOf(top, { frame = FRAME, scale = SCALE, phone = PHONE } = {}) {
  if (top < 0 || top + frame.height > phone.height) throw new Error(`A part from ${top} px does not fit the phone's ${phone.height} px.`);
  return [0, top * scale, frame.width * scale, (top + frame.height) * scale];
}

export function parseArguments(argv) {
  const options = { out: SOURCES };
  for (let index = 0; index < argv.length; index++) {
    if (argv[index] === '--out' && index + 1 < argv.length) options.out = argv[++index];
    else throw new Error(`Usage: node scripts/ux/round.mjs [--out <folder>]; ${argv[index]} is not understood.`);
  }
  return options;
}

async function main(argv) {
  const { out } = parseArguments(argv);
  const folder = resolve(root, out), work = await mkdtemp(join(tmpdir(), 'ux-round-'));
  await mkdir(folder, { recursive: true });
  const { chromium } = playwright();
  const browser = await chromium.launch({ executablePath: process.env.STUDIO_CHROMIUM || undefined });
  const jobs = [];
  try {
    const showcase = await buildShowcase(join(work, 'dist'));
    const match = JOURNEYS.find(journey => journey.id === 'match');
    const screens = await walkJourney(browser, match, PHONE, { showcase, scale: SCALE });
    for (const { name, screen, top } of SCREENS) {
      const shot = screens.find(one => one.name === screen);
      const png = join(work, `${name}.png`);
      await writeFile(png, shot.png);
      jobs.push({ png, box: boxOf(top), out: join(folder, `${name}.webp`) });
    }
    const table = await serveTable();
    const context = await browser.newContext({ viewport: PHONE, deviceScaleFactor: SCALE, isMobile: true, hasTouch: true });
    try {
      const page = await context.newPage();
      await openBench(page, table.base);
      const last = CAST.frames.at(-1), { interval } = CAST;
      const shots = await filmFrames(page, { spell: CAST.spell, damage: CAST.damage, frames: last / interval + 1, interval });
      for (const [index, ms] of CAST.frames.entries()) {
        const shot = shots.find(one => one.ms === ms);
        if (!shot) throw new Error(`The bench filmed no frame at ${ms} ms.`);
        const png = join(work, `${castFrame(index)}.png`);
        await writeFile(png, shot.png);
        jobs.push({ png, out: join(folder, `${castFrame(index)}.webp`) });
      }
    } finally {
      await context.close();
      await table.close();
    }
    cut(jobs);
    for (const job of jobs) console.log(job.out);
  } finally {
    await browser.close();
    await rm(work, { recursive: true, force: true });
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
