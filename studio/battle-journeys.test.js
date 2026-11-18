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

// A battle spec reads its mock route's `shown` or `writes` through `expect.poll` (studio/AGENTS.md, #816, #831, #845):
// the request reaches the route some time after the click. A plain read that expects something was sent follows,
// in the same test, a poll on that route state or a wait only the host's answer ends. A negative read stays plain.
const STATE = /(?<![\w'"`.])(?:[\w$]+\.)*(?:shown|writes)\b(?![='"`])/;
const POLLED = /expect\.poll\(\s*(?:async\s*)?\(\)\s*=>\s*(?:\[\.\.\.)?(?:[\w$]+\.)*(?:shown|writes)\b/;
// Waits that only the host's answer ends: a refusal's text, the problem a notice test reports, a URL the answer chose.
const ANSWERED = [/-error'\)\)\.(?:not\.)?to(?:Have|Contain)Text\(/, /#notices-problem'\)\)\.toContainText\(/, /\.waitForURL\(/];
const TEST = /^\s*(?:for \(.*\) )?test(?:\.(?:only|skip|fixme))?\(/;

/** Where the parenthesis opened at `open` closes on the line, or -1. */
function closing(line, open) {
  let depth = 0;
  for (let at = open; at < line.length; at++) {
    if (line[at] === '(') depth++;
    else if (line[at] === ')' && --depth === 0) return at;
  }
  return -1;
}

/** The plain reads of the route state that expect something was sent, with nothing before them that makes them safe. */
function unpolled(lines) {
  const found = [];
  lines.forEach((line, index) => {
    for (const match of line.matchAll(/(?<![.\w])expect\(/g)) {
      const open = match.index + 'expect'.length, close = closing(line, open);
      const subject = close < 0 ? '' : line.slice(open + 1, close);
      if (!STATE.test(subject) || /\bawait\b|^page\b/.test(subject)) continue;
      const rest = line.slice(close + 1), matcher = rest.match(/^\.(?:not\.)?(\w+)\(/);
      const expected = matcher ? rest.slice(matcher[0].length, closing(rest, matcher[0].length - 1)) : '';
      if (matcher && ((matcher[1] === 'toEqual' && expected === '[]') || (matcher[1] === 'toHaveLength' && expected === '0'))) continue;
      let start = index;
      while (start > 0 && !TEST.test(lines[start])) start--;
      const before = [...lines.slice(start, index), line.slice(0, match.index)].join('\n');
      if (POLLED.test(before) || ANSWERED.some((wait) => wait.test(before))) continue;
      found.push({ line: index + 1, read: `expect(${subject})${matcher ? `.${matcher[1]}` : ''}` });
    }
  });
  return found;
}

// What a line-based reading cannot see, each with the wait that makes the plain read safe.
// The tempo sheet stays open while its batch is on the wire, and sends each tempo only once the host answered that
// asking's `shown` (table/battle-tempo.js pump): once it closes, every write and every `shown` of the batch has reached
// the route.
const READ_AFTER_HOST = [
  { file: 'battle-tempo.spec.js', read: 'expect(h.writes).toEqual', why: 'the tempo sheet closed after its batch' },
  { file: 'battle-tempo.spec.js', read: 'expect([...new Set(h.shown)]).toEqual', why: 'the tempo sheet closed after its batch' },
  { file: 'battle-parity.spec.js', read: 'expect(state.writes.slice(1).map(one => one.speed)).toEqual', why: 'the tempo sheet closed after its batch' },
  { file: 'battle-parity.spec.js', read: 'expect(state.writes.length).toBe', why: 'the tempo tip shows only once the host served the asking after the mutation' },
  { file: 'battle-tutorial.spec.js', read: 'expect(state.writes.map(one => [one.kind, one.creature, one.targets])).toEqual', why: 'playStep polls writes.length at the end of each step' },
];

test('a battle journey polls its route for what was sent, unless the host has answered first', () => {
  const specs = journeys.filter(({ file }) => file.endsWith('.spec.js'));
  const flagged = specs.flatMap(({ file, lines }) => unpolled(lines).map((one) => ({ file, ...one })));
  const allowed = (one) => READ_AFTER_HOST.some((entry) => entry.file === one.file && entry.read === one.read);
  assert.deepEqual(flagged.filter((one) => !allowed(one)).map((one) => `${one.file}:${one.line} ${one.read}`), []);
  // An entry that no longer matches a read is stale: remove it, so that it cannot excuse a later one.
  assert.deepEqual(READ_AFTER_HOST.filter((entry) => !flagged.some((one) => one.file === entry.file && one.read === entry.read)).map((entry) => entry.read), []);
});

test('the route rule passes a poll, a host-answered wait and a negative read, and flags a read after a click', () => {
  const head = "test('a', async ({ page }) => {";
  assert.deepEqual(unpolled([head, "  await page.locator('#planning-confirm').click();", '  expect(h.writes).toEqual([{ kind: 1 }]);']),
    [{ line: 3, read: 'expect(h.writes).toEqual' }]);
  assert.deepEqual(unpolled([head, '  await page.click(); expect(h.state.writes[0].tier).toBe(2);']), [{ line: 2, read: 'expect(h.state.writes[0].tier).toBe' }]);
  assert.deepEqual(unpolled([head, '  expect(h.writes).toEqual([]); expect(h.shown).toHaveLength(0);']), []);
  assert.deepEqual(unpolled([head, '  await expect.poll(() => h.state.writes.length).toBe(1); expect(h.state.writes[0]).toEqual({});']), []);
  assert.deepEqual(unpolled([head, "  await expect(page.locator('#combat-error')).toHaveText(m);", '  expect(state.writes).toHaveLength(1);']), []);
  assert.deepEqual(unpolled([head, '  await page.waitForURL(/next$/);', '  expect(h.writes).toEqual([{}]);']), []);
  // A poll after the read, or in the test before, does not make it safe; the reads of a request's query are not the state.
  assert.deepEqual(unpolled([head, '  await expect.poll(() => h.writes.length).toBe(1);', '});', head, '  expect(h.writes).toEqual([{}]);',
    '  await expect.poll(() => h.writes.length).toBe(1);']), [{ line: 5, read: 'expect(h.writes).toEqual' }]);
  assert.deepEqual(unpolled([head, "  expect(h.reads.filter(one => !one.query.includes('shown=')).length).toBe(1);"]), []);
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
