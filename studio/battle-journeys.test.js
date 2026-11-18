// What the battle journeys (studio/browser/battle-*.spec.js) must hold to stay green on a loaded runner, read from
// their source in Node like the pages' own tests (ADR 0024). Each rule is a flake that has already failed CI once.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import * as timing from '../table/battle-timing.js';

const folder = join(dirname(fileURLToPath(import.meta.url)), 'browser');
const journeys = readdirSync(folder)
  .filter((file) => /^battle-.*(\.spec|-fixture|-scene)\.js$/.test(file))
  .map((file) => ({ file, lines: readFileSync(join(folder, file), 'utf8').split('\n') }));

// A second of fake clock moved frame by frame: about 2.3 s of real time on an idle machine, for the 1440 × 1000
// scene drawn in software, and several times that on a loaded runner (#749, #784).
const LONG_MS = 1000;
const MINUTE_MS = 60000;

/** The milliseconds a `runFor` argument names, from numbers and the playback's timings; null when it reads a variable. */
function duration(argument) {
  const names = argument.match(/[A-Za-z_]\w*/g) ?? [];
  if (names.some((name) => typeof timing[name] !== 'number')) return null;
  const value = names.reduce((text, name) => text.replaceAll(name, String(timing[name])), argument);
  return /^[\d\s+*()-]+$/.test(value) ? Function(`return (${value});`)() : null;
}

/** The longest budget a test gives itself, or the file gives every test, for the line at `index`. */
function budget(lines, index) {
  const timeouts = (from, to) => lines.slice(from, to + 1)
    .flatMap((line) => [...line.matchAll(/test\.setTimeout\((\d+)\)/g)].map((match) => Number(match[1])));
  const file = lines.map((line) => line.match(/^test\.setTimeout\((\d+)\)/)).filter(Boolean).map((match) => Number(match[1]));
  let start = index;
  while (start > 0 && !/^(?:for \(.*\) )?test\(/.test(lines[start]) && !/^\S/.test(lines[start])) start--;
  // A helper's clock moves inside whichever test calls it: only a budget for the whole file covers it.
  const own = /^(?:for \(.*\) )?test\(/.test(lines[start]) ? timeouts(start, index) : [];
  return Math.max(30000, ...file, ...own);
}

test('a battle journey that moves the fake clock a second or more frame by frame gives its test a minute', () => {
  const short = [];
  for (const { file, lines } of journeys) {
    lines.forEach((line, index) => {
      for (const match of line.matchAll(/clock\.runFor\(([^;]*?)\);/g)) {
        const ms = duration(match[1]);
        if (ms !== null && ms >= LONG_MS && budget(lines, index) < MINUTE_MS) short.push(`${file}:${index + 1} runFor(${match[1]})`);
      }
    });
  }
  // Jump from one playback timer to the next with fastForward where only the stages matter (#779), or say why the
  // frames between them are needed and give the test 60 s.
  assert.deepEqual(short, []);
});

test('a battle journey takes the first board, not the hidden loading card, for the page having connected', () => {
  const early = [];
  for (const { file, lines } of journeys) {
    lines.forEach((line, index) => {
      if (!/goto\('\/table\/battle\.html'\)|page\.reload\(\)/.test(line)) return;
      const at = [index, index + 1].find((one) => lines[one]?.includes("#loading')).toBeHidden()"));
      if (at === undefined) return;
      // The page puts its loading card away before it asks for the first board (live.ts choose): the connection is
      // live only once that board has been applied (battle-live.js), whatever its label reads while a result plays.
      const next = [lines[at], lines[at + 1] ?? ''].join('\n');
      if (!next.includes("#connection')).toHaveText(/^LIVE/)") && !next.includes("toHaveAttribute('data-connection', 'live')")) early.push(`${file}:${at + 1}`);
    });
  }
  assert.deepEqual(early, []);
});

test('the clock rule reads the playback timings and the budget of the test or the file', () => {
  assert.equal(duration('WINDUP_MS + IMPACT_MS'), timing.WINDUP_MS + timing.IMPACT_MS);
  assert.equal(duration('2 * (WINDUP_MS + SETTLE_MS)'), 2 * (timing.WINDUP_MS + timing.SETTLE_MS));
  assert.equal(duration('finishAt - impactAt - 1'), null);
  const lines = ["test('a', async () => {", '  test.setTimeout(60000);', '  await page.clock.runFor(3000);', '});',
    'async function helper(page) {', '  await page.clock.runFor(3000);', '}', "test('b', async () => {", '  await page.clock.runFor(3000);', '});'];
  assert.equal(budget(lines, 2), 60000);
  assert.equal(budget(lines, 5), 30000);
  assert.equal(budget(lines, 8), 30000);
  assert.equal(budget(['test.setTimeout(90000);', ...lines], 9), 90000);
});
