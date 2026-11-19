#!/usr/bin/env node
// The frame time of a staging (docs/art/battle-craft-plan.md, BC10): each effect played on the effects bench
// (`/battle-lab?bench`) in Chromium at phone size, the page's processor slowed by the browser's own throttle,
// on as many figures as the content lets the spell reach. Two measures an effect:
//
// - the step: what one step of the game costs the page's thread, the scene's update and the drawing calls it
//   issues, on the film's clock, so every run steps the same frames. The throttle slows this thread, and this is
//   the number that says what a staging's code costs a slow phone.
// - the frame: the time between two animation frames while the effect plays on the page's own loop. It holds
//   what the renderer does with the calls, which the throttle does not slow. On a machine with no graphics card
//   that is a software renderer, and the number compares one staging with another on that machine: it is no
//   phone's frame rate (docs/art/battle-acceptance.md).
//
//   node scripts/battle/frametime.mjs spell:pummel:v1                 # one effect, the measures printed
//   node scripts/battle/frametime.mjs --all --out artifacts/frametime.json
//   node scripts/battle/frametime.mjs --all --markdown                # the record's table
//   node scripts/battle/frametime.mjs --all --throttle 6 --runs 5 --speed 2
//
// It serves `table/` itself; the bundle must be built first (`npm --prefix battle run build`).

import { mkdir, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { FILM_TICK_MS } from '../../table/battle-bench.js';
import { castTargets } from './cast.mjs';
import { openBench, playwright, serveTable } from './filmstrip.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const PHONE = { width: 390, height: 844 };
/** One frame of a sixty-hertz screen. */
export const FRAME_MS = 1000 / 60;
// A frame that took two frames or more was seen to stutter: the count the record gives for the page's own loop.
const LONG_FRAME_MS = 2 * FRAME_MS - 1;
// The health the fixture says each creature struck loses: a medium blow, as the stagings' spec plays.
const DAMAGE = 5;
const SPEEDS = [0.5, 1, 2];
// The bench's samples measured beside the spells: the motif alone, the field's particles at their cap, one halo.
const BASELINES = ['kit:plain', 'kit:budget', 'kit:pulse'];

const sorted = samples => [...samples].sort((one, other) => one - other);
const centre = ordered => ordered.length % 2 ? ordered[(ordered.length - 1) / 2] : (ordered[ordered.length / 2 - 1] + ordered[ordered.length / 2]) / 2;

/** What a list of times says: the median, the 95th centile, the worst, and how many are over `budget`. */
export function summarize(samples, budget = FRAME_MS) {
  if (samples.length === 0) throw new Error('There is no sample to summarize.');
  const ordered = sorted(samples);
  return { count: ordered.length, median: centre(ordered), p95: ordered[Math.min(ordered.length - 1, Math.ceil(ordered.length * 0.95) - 1)],
    worst: ordered.at(-1), over: ordered.filter(one => one > budget).length };
}

/** Several runs of one measure read as one: for each number, the middle one of the runs. */
export function middle(runs) {
  return Object.fromEntries(Object.keys(runs[0]).map(key => [key, centre(sorted(runs.map(run => run[key])))]));
}

/**
 * What each step of the game costs the page's thread while one effect plays, in milliseconds: the bench's film
 * clock is stepped a tick at a time over the whole action, and each step is timed where it runs.
 */
export async function measureSteps(page, { spell, actor = 'a1', targets, speed = 1 } = {}) {
  return page.evaluate(([options, tick]) => {
    const bench = window.battleBench;
    const { duration } = bench.film.start(options);
    const samples = [];
    try {
      for (let played = 0; played < duration; played += tick) {
        const before = performance.now();
        bench.film.advance(tick);
        samples.push(performance.now() - before);
      }
    } finally { bench.film.stop(); }
    return samples;
  }, [{ spell, actor, targets, speed, damage: DAMAGE }, FILM_TICK_MS]);
}

/**
 * The time between two animation frames while one effect plays on the page's own loop, from the play to the
 * resolved action. Returns the intervals in milliseconds.
 */
export async function measureFrames(page, { spell, actor = 'a1', targets, speed = 1 } = {}) {
  await page.evaluate(([options, rate]) => {
    const select = document.getElementById('playback-speed');
    select.value = String(rate);
    select.dispatchEvent(new Event('change'));
    window.battleBench.choose(options);
  }, [{ spell, actor, targets, damage: DAMAGE }, speed]);
  return page.evaluate(() => new Promise((done, failed) => {
    const lab = document.getElementById('lab');
    const intervals = [];
    // An effect that never resolves is a fault of the bench: the measure says so and does not wait for ever.
    const late = setTimeout(() => failed(new Error(`The effect did not resolve: the bench stands at ${lab.dataset.stage}.`)), 30000);
    let last = 0;
    const frame = now => {
      intervals.push(now - last);
      last = now;
      if (lab.dataset.stage === 'resolved') { clearTimeout(late); done(intervals); } else requestAnimationFrame(frame);
    };
    requestAnimationFrame(now => { last = now; window.battleBench.play(); requestAnimationFrame(frame); });
  }));
}

/** One effect measured `runs` times: its steps and its frames, each number the middle one of the runs. */
export async function measureEffect(page, { spell, targets, speed = 1, runs = 3 } = {}) {
  const steps = [], frames = [];
  // One run after the other, on purpose: they are all played on the one bench this page holds.
  for (let run = 0; run < runs; run++) {
    steps.push(summarize(await measureSteps(page, { spell, targets, speed })));
    frames.push(summarize(await measureFrames(page, { spell, targets, speed }), LONG_FRAME_MS));
  }
  return { step: middle(steps), frame: middle(frames) };
}

const decimal = number => number.toFixed(1);

/** The record's table (docs/art/battle-acceptance.md), a row an effect. ◌ marks a staging that holds a `pulse`. */
export function recordTable(results) {
  const head = '| Effect | Figures | Beats | Step, median | Step, 95th | Step, worst | Steps over a frame | Frame, median | Frame, worst | Long frames |\n| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |';
  const rows = results.map(({ name, targets, beats, pulse, step, frame }) =>
    `| ${name}${pulse ? ' ◌' : ''} | ${targets.length} | ${beats} | ${decimal(step.median)} | ${decimal(step.p95)} | ${decimal(step.worst)} | ${step.over} | ${decimal(frame.median)} | ${decimal(frame.worst)} | ${frame.over} |`);
  return `${[head, ...rows].join('\n')}\n`;
}

// The flags that take a value: what each must be, and its name on the options.
const VALUED = {
  '--throttle': { name: 'throttle', accepts: number => number >= 1, says: '--throttle is the browser\'s CPU slowdown, 1 (none) or more.' },
  '--runs': { name: 'runs', accepts: number => Number.isInteger(number) && number >= 1, says: '--runs is a whole number of runs, 1 or more.' },
  '--speed': { name: 'speed', accepts: number => SPEEDS.includes(number), says: '--speed is 0.5, 1 or 2, as the page offers.' },
};
const TEXTS = { '--out': 'out', '--url': 'base' };

export function parseArguments(argv) {
  const options = { spells: [], all: false, throttle: 4, runs: 3, speed: 1, out: null, base: null, markdown: false };
  for (let index = 0; index < argv.length; index++) {
    const word = argv[index];
    if (word === '--all') options.all = true;
    else if (word === '--markdown') options.markdown = true;
    else if (Object.hasOwn(VALUED, word) || Object.hasOwn(TEXTS, word)) {
      index += 1;
      if (index >= argv.length) throw new Error(`${word} needs a value.`);
      if (Object.hasOwn(TEXTS, word)) { options[TEXTS[word]] = argv[index]; continue; }
      const { name, accepts, says } = VALUED[word];
      const number = Number(argv[index]);
      if (!Number.isFinite(number) || !accepts(number)) throw new Error(says);
      options[name] = number;
    } else if (word.startsWith('--')) throw new Error(`Unknown option ${word}.`);
    else options.spells.push(word);
  }
  if (options.all === options.spells.length > 0) throw new Error('Name one or more effects (spell:pummel:v1), or --all.');
  return options;
}

// What draws the canvas: a phone's number is read against it, and a software renderer's is not a phone's.
const renderer = page => page.evaluate(() => {
  const gl = document.createElement('canvas').getContext('webgl2');
  const named = gl?.getExtension('WEBGL_debug_renderer_info');
  return gl ? String(gl.getParameter(named ? named.UNMASKED_RENDERER_WEBGL : gl.RENDERER)) : 'no WebGL';
});

async function main(argv) {
  const options = parseArguments(argv);
  const { chromium } = playwright();
  const served = options.base ? null : await serveTable();
  let browser;
  try {
    browser = await chromium.launch({ executablePath: process.env.STUDIO_CHROMIUM || undefined });
    const context = await browser.newContext({ viewport: PHONE, isMobile: true, hasTouch: true });
    const page = await context.newPage();
    await openBench(page, options.base ?? served.base);
    const session = await context.newCDPSession(page);
    await session.send('Emulation.setCPUThrottlingRate', { rate: options.throttle });
    const listed = await page.evaluate(() => [...window.battleBench.entries, ...window.battleBench.samples].map(({ id, name, staging }) => ({ id, name, staging: staging ?? [] })));
    const targetsOf = castTargets(root);
    // With --all: every spell, and the three samples the record reads the spells against.
    const asked = options.all ? listed.filter(one => !one.id.startsWith('kit:') || BASELINES.includes(one.id)) : options.spells.map(id => listed.find(one => one.id === id) ?? { id, name: id, staging: [] });
    const effects = [];
    for (const { id, name, staging } of asked) {
      // A sample of the kit is no content: it is played on the three figures across the field, the most a spell reaches.
      const targets = id.startsWith('kit:') ? ['b1', 'b2', 'b3'] : targetsOf(id);
      const measured = await measureEffect(page, { spell: id, targets, speed: options.speed, runs: options.runs });
      effects.push({ id, name, targets, beats: staging.length, pulse: staging.some(beat => beat.beat === 'pulse'), ...measured });
      if (!options.markdown) console.error(`${id}: step ${decimal(measured.step.median)} ms (worst ${decimal(measured.step.worst)}), frame ${decimal(measured.frame.median)} ms (worst ${decimal(measured.frame.worst)})`);
    }
    const record = { viewport: PHONE, throttle: options.throttle, runs: options.runs, speed: options.speed, renderer: await renderer(page), userAgent: await page.evaluate(() => navigator.userAgent), effects };
    if (options.out) {
      const path = resolve(options.out);
      await mkdir(dirname(path), { recursive: true });
      await writeFile(path, `${JSON.stringify(record, null, 2)}\n`);
      console.error(path);
    }
    console.log(options.markdown ? recordTable(effects) : JSON.stringify(record, null, 2));
  } finally {
    await browser?.close();
    await served?.close();
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
