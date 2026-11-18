// What the battle journeys (studio/browser/battle-*.spec.js) must hold to stay green on a loaded runner, read from
// their source in Node like the pages' own tests (ADR 0024). Each rule comes from a flake that has already failed CI.

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

// One second of fake clock moved frame by frame takes about 2.3 s of real time on an idle machine, drawing the
// 1440 × 1000 scene in software. It takes several times that on a loaded runner (#749, #784).
const LONG_MS = 1000;
const MINUTE_MS = 60000;

/**
 * The milliseconds a `runFor` argument names, from numbers and the playback's timings: null when it reads a variable
 * this test cannot know, NaN when it is written in a form the rule cannot read, so that it is not passed unread.
 */
function duration(argument) {
  const tokens = argument.match(/\d+|[A-Za-z_$][\w$]*|\S/g) ?? [];
  if (tokens.some((token) => /^[A-Za-z_$]/.test(token) && typeof timing[token] !== 'number')) return null;
  let at = 0;
  // Sums of products of numbers, timings and parentheses: all a journey writes, read without evaluating code.
  const sum = () => { let value = product(); while (tokens[at] === '+' || tokens[at] === '-') value = tokens[at++] === '+' ? value + product() : value - product(); return value; };
  const product = () => { let value = factor(); while (tokens[at] === '*') { at++; value *= factor(); } return value; };
  const factor = () => {
    const token = tokens[at++];
    if (token === '-') return -factor();
    if (token === '(') { const value = sum(); if (tokens[at++] !== ')') throw new SyntaxError(argument); return value; }
    if (/^\d+$/.test(token ?? '')) return Number(token);
    if (typeof timing[token] === 'number') return timing[token];
    throw new SyntaxError(argument);
  };
  try { const value = sum(); return at === tokens.length ? value : Number.NaN; } catch { return Number.NaN; }
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
        if (Number.isNaN(ms)) short.push(`${file}:${index + 1} runFor(${match[1]}) is not read`);
        else if (ms !== null && ms >= LONG_MS && budget(lines, index) < MINUTE_MS) short.push(`${file}:${index + 1} runFor(${match[1]})`);
      }
    });
  }
  // Jump from one playback timer to the next with fastForward where only the stages matter (#779), or say why the
  // frames between them are needed and give the test 60 s.
  assert.deepEqual(short, []);
});

// What opens the battle page or a seat on it: a load, a reload, a seat chosen on a device that holds both, or a tab
// back in view, which chooses its seat again (live.ts).
const OPENS = new RegExp([/goto\(['"`]\/table\/battle\.html/, /page\.reload\(\)/, /#seat-choices button'\)\.(?:first\(\)|last\(\)|nth\(\d+\))\.click\(\)/,
  /visibility\(false\)/, /value: false \}\); document\.dispatchEvent\(new Event\('visibilitychange'\)\)/].map((one) => one.source).join('|'));
const LOADED = "#loading')).toBeHidden()";
const LIVE = ["#connection')).toHaveText(/^LIVE/)", "toHaveAttribute('data-connection', 'live')"];

/** The lines (1-based) where a page opened is taken as connected because its loading card is hidden. */
function loadedOnly(lines) {
  const early = [];
  lines.forEach((line, index) => {
    if (!OPENS.test(line)) return;
    const at = [0, 1, 2, 3].map((step) => index + step).find((one) => lines[one]?.includes(LOADED));
    if (at === undefined) return;
    // The page puts its loading card away before it asks for the first board (live.ts choose): the connection is
    // live only once that board has been applied (battle-live.js), whatever its label reads while a result plays.
    // The window ends where the page is opened again: a later open's wait says nothing of this one.
    const again = lines.findIndex((one, step) => step > at && OPENS.test(one));
    const next = lines.slice(at, Math.min(at + 6, again === -1 ? lines.length : again)).join('\n');
    if (!LIVE.some((wait) => next.includes(wait))) early.push(at + 1);
  });
  return early;
}

test('a battle journey waits for the live connection, not the hidden loading card, once it opens the page', () => {
  const early = journeys.flatMap(({ file, lines }) => loadedOnly(lines).map((line) => `${file}:${line}`));
  assert.deepEqual(early, []);
});

test('the connection rule reads a load, a reload, a seat chosen and a tab back in view, each on its own', () => {
  const live = "  await expect(page.locator('#battle')).toHaveAttribute('data-connection', 'live');";
  assert.deepEqual(loadedOnly(['  await page.goto(`/table/battle.html${query}`);', '  await ready(page);', `  await expect(page.locator('#loading')).toBeHidden();`]), [3]);
  assert.deepEqual(loadedOnly([`  await page.reload(); await expect(page.locator('#loading')).toBeHidden();`, live]), []);
  assert.deepEqual(loadedOnly([`  await page.locator('#seat-choices button').nth(1).click(); await expect(page.locator('#loading')).toBeHidden();`]), [1]);
  assert.deepEqual(loadedOnly(['  await page.goto("/table/battle.html");', `  await expect(page.locator('#loading')).toBeHidden();`]), [2]);
  assert.deepEqual(loadedOnly([`  await visibility(false); await expect(page.locator('#loading')).toBeHidden();`]), [1]);
  // A reload's wait does not clear the load before it.
  assert.deepEqual(loadedOnly([`  await page.goto('/table/battle.html'); await expect(page.locator('#loading')).toBeHidden();`,
    `  await page.reload(); await expect(page.locator('#loading')).toBeHidden();`, live]), [1]);
});

test('the clock rule reads the playback timings and the budget of the test or the file', () => {
  assert.equal(duration('WINDUP_MS + IMPACT_MS'), timing.WINDUP_MS + timing.IMPACT_MS);
  assert.equal(duration('2 * (WINDUP_MS + SETTLE_MS)'), 2 * (timing.WINDUP_MS + timing.SETTLE_MS));
  assert.equal(duration('finishAt - impactAt - 1'), null);
  assert.equal(duration('globalThis.process.exit(1)'), null);
  assert.ok(Number.isNaN(duration('(WINDUP_MS')));
  assert.ok(Number.isNaN(duration("'00:03'")));
  assert.equal(duration('-1000 + 2000'), 1000);
  const lines = ["test('a', async () => {", '  test.setTimeout(60000);', '  await page.clock.runFor(3000);', '});',
    'async function helper(page) {', '  await page.clock.runFor(3000);', '}', "test('b', async () => {", '  await page.clock.runFor(3000);', '});'];
  assert.equal(budget(lines, 2), 60000);
  assert.equal(budget(lines, 5), 30000);
  assert.equal(budget(lines, 8), 30000);
  assert.equal(budget(['test.setTimeout(90000);', ...lines], 9), 90000);
});
