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
// the request reaches the route some time after the click. A plain read that expects something was sent must come
// after a poll on that same route state, or after a wait that only the host's answer ends, with no action between that
// wait and the read. A negative read stays plain.
const STATE = /(?<![\w$'"`]|[\w$]\.)(?:[\w$]+\.)*(shown|writes)\b(?![='"`])/;
const POLLED = (field) => new RegExp(`expect\\.poll\\(\\s*(?:async\\s*)?\\(\\)\\s*=>\\s*(?:\\[\\.\\.\\.)?(?:[\\w$]+\\.)*${field}\\b`);
// Waits that only the host's answer ends: a refusal's text, the problem a notice test reports, a URL the answer chose.
const ANSWERED = [/-error'\)\)\.to(?:Have|Contain)Text\(/, /#notices-problem'\)\)\.toContainText\(/, /\.waitForURL\(/];
// What can send: a wait before the last of these says nothing of what it sent. A helper the spec awaits may click.
const ACTION = /\.(?:click|dblclick|press|pressSequentially|type|fill|check|tap|selectOption|dispatchEvent)\(|await (?!expect\b)[A-Za-z_$][\w$]*\(/g;
const TEST = /^\s*(?:for \(.*\) )?test(?:\.(?:only|skip|fixme|beforeEach|afterEach|beforeAll|afterAll|describe))?\(/;
// Where a helper starts: the scan back from a read stops there, so that it does not borrow the test before it.
const TOP = /^(?:export )?(?:async )?function\b|^(?:export )?const\b/;
const NEGATIVE = new Set(['toEqual([])', 'toStrictEqual([])', 'toHaveLength(0)']);

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
      let start = index;
      while (start > 0 && !TEST.test(lines[start]) && !TOP.test(lines[start])) start--;
      const title = lines[start].match(/test(?:\.\w+)?\((['"`])(.*?)\1/)?.[2] ?? '';
      if (close < 0) {
        // A subject this reading cannot close is not passed unread.
        if (STATE.test(line.slice(open))) found.push({ line: index + 1, title, read: 'expect( not closed on its line' });
        continue;
      }
      const subject = line.slice(open + 1, close), field = subject.match(STATE)?.[1];
      if (!field || /\bawait\b|^page\b/.test(subject)) continue;
      const rest = line.slice(close + 1), matcher = rest.match(/^\.(not\.)?(\w+)\(/);
      const expected = matcher ? rest.slice(matcher[0].length, closing(rest, matcher[0].length - 1)).replace(/\s+/g, '') : '';
      if (matcher && !matcher[1] && (NEGATIVE.has(`${matcher[2]}(${expected})`) || (/\.length$/.test(subject) && matcher[2] === 'toBe' && expected === '0'))) continue;
      const lead = [...lines.slice(start, index), line.slice(0, match.index)].join('\n');
      const actions = [...lead.matchAll(ACTION)], since = actions.length ? lead.slice(actions.at(-1).index) : lead;
      if (POLLED(field).test(lead) && (!actions.length || POLLED(field).test(since))) continue;
      if (ANSWERED.some((wait) => wait.test(since))) continue;
      found.push({ line: index + 1, title, read: `expect(${subject})${matcher ? `.${matcher[1] ?? ''}${matcher[2]}` : ''}` });
    }
  });
  return found;
}

// What a line-based reading cannot see, each by its test and its read, with the wait that makes the plain read safe.
// The tempo sheet stays open while its batch is on the wire, and sends each tempo only after the host has answered that
// asking's `shown` (table/battle-tempo.js pump). Once the sheet closes, every write and every `shown` of the batch has
// reached the route.
const READ_AFTER_HOST = [
  { file: 'battle-tempo.spec.js', title: '${count} creature tabs keep an editable team draft and send once per asking',
    read: 'expect(h.writes).toEqual', why: 'the tempo sheet closed after its batch' },
  { file: 'battle-tempo.spec.js', title: '${count} creature tabs keep an editable team draft and send once per asking',
    read: 'expect([...new Set(h.shown)]).toEqual', why: 'the tempo sheet closed after its batch' },
  { file: 'battle-tempo.spec.js', title: 'a refusal after one accepted creature leaves a reviewable draft and requires another confirmation',
    read: 'expect(h.writes.map(one => one.creature)).toEqual', why: 'the tempo sheet closed after its batch' },
  { file: 'battle-tempo.spec.js', title: 'a lost response after acceptance reconciles without resending the accepted creature',
    read: 'expect(h.writes.map(one => one.creature)).toEqual', why: 'the tempo sheet closed after its batch' },
  { file: 'battle-tempo.spec.js', title: 'backgrounding during a write clears the batch and reopening shows only authoritative remaining choices',
    read: 'expect(h.writes.length).toBe', why: 'after a poll that saw one write, a read that nothing more was sent: it stays plain, as a negative read does' },
  { file: 'battle-parity.spec.js', title: 'keyboard journey reads energy, compares a mutation, edits every tempo and reopens applied damage',
    read: 'expect(state.writes.length).toBe', why: 'the tempo tip shows only after the host served the asking that follows the mutation' },
  { file: 'battle-parity.spec.js', title: 'keyboard journey reads energy, compares a mutation, edits every tempo and reopens applied damage',
    read: 'expect(state.writes.slice(1).map(one => one.speed)).toEqual', why: 'the tempo sheet closed after its batch' },
  { file: 'battle-parity.spec.js', title: 'keyboard journey reads energy, compares a mutation, edits every tempo and reopens applied damage',
    read: 'expect(state.writes.every(one => state.shown.includes(one.asked))).toBe', why: 'each write the page sends follows its own answered shown: a write still on the wire cannot fail it' },
  { file: 'battle-tutorial.spec.js', title: 'tutorial 1 is played to its end: each step shows its coach card and opens its concept cards',
    read: 'expect(state.writes.map(one => [one.kind, one.creature, one.targets])).toEqual', why: 'playStep polls writes.length at the end of each step' },
];

test('a battle journey polls its route for what was sent, unless the host has answered since its last action', () => {
  const specs = journeys.filter(({ file }) => file.endsWith('.spec.js'));
  const flagged = specs.flatMap(({ file, lines }) => unpolled(lines).map((one) => ({ file, ...one })));
  const matches = (entry, one) => entry.file === one.file && entry.title === one.title && entry.read === one.read;
  assert.deepEqual(flagged.filter((one) => !READ_AFTER_HOST.some((entry) => matches(entry, one))).map((one) => `${one.file}:${one.line} ${one.read}`), []);
  // Each entry excuses exactly one read: one that matches none is stale, and one that matches more excuses a read it never saw.
  assert.deepEqual(READ_AFTER_HOST.filter((entry) => flagged.filter((one) => matches(entry, one)).length !== 1)
    .map((entry) => `${entry.file} ${entry.read}: matches ${flagged.filter((one) => matches(entry, one)).length} reads, not 1`), []);
});

test('the route rule passes a poll, a host-answered wait and a negative read, and flags a read after a click', () => {
  const head = "test('a', async ({ page }) => {", at = (line, read) => [{ line, title: 'a', read }];
  assert.deepEqual(unpolled([head, "  await page.locator('#planning-confirm').click();", '  expect(h.writes).toEqual([{ kind: 1 }]);']), at(3, 'expect(h.writes).toEqual'));
  assert.deepEqual(unpolled([head, '  await page.click(); expect(h.state.writes[0].tier).toBe(2);']), at(2, 'expect(h.state.writes[0].tier).toBe'));
  assert.deepEqual(unpolled([head, '  expect(h.writes).toEqual([]); expect(h.shown).toHaveLength(0); expect(h.writes.length).toBe(0); expect(h.writes).toStrictEqual([ ]);']), []);
  assert.deepEqual(unpolled([head, '  await expect.poll(() => h.state.writes.length).toBe(1); expect(h.state.writes[0]).toEqual({});']), []);
  assert.deepEqual(unpolled([head, "  await page.click(); await expect(page.locator('#combat-error')).toHaveText(m);", '  expect(state.writes).toHaveLength(1);']), []);
  assert.deepEqual(unpolled([head, '  await page.click(); await page.waitForURL(/next$/);', '  expect(h.writes).toEqual([{}]);']), []);
  // A poll before the last action, a poll on the other field, or a poll in the test before does not make a read safe.
  assert.deepEqual(unpolled([head, '  await expect.poll(() => h.writes.length).toBe(1); await page.click(); expect(h.writes[1]).toEqual({});']), at(2, 'expect(h.writes[1]).toEqual'));
  assert.deepEqual(unpolled([head, "  await page.click(); await expect(page.locator('#combat-error')).toHaveText(m); await page.click();", '  expect(h.writes).toEqual([{}]);']), at(3, 'expect(h.writes).toEqual'));
  assert.deepEqual(unpolled([head, '  await expect.poll(() => h.shown.length).toBe(1); expect(h.writes).toEqual([{}]);']), at(2, 'expect(h.writes).toEqual'));
  assert.deepEqual(unpolled([head, '  await expect.poll(() => h.writes.length).toBe(1);', '});', 'async function helper(h) {', '  expect(h.writes).toEqual([{}]);', '}']),
    [{ line: 5, title: '', read: 'expect(h.writes).toEqual' }]);
  // A negated negative read expects something was sent; a spread and an unclosed subject are read too.
  assert.deepEqual(unpolled([head, '  expect(h.writes).not.toEqual([]);']), at(2, 'expect(h.writes).not.toEqual'));
  assert.deepEqual(unpolled([head, '  expect([...h.state.shown]).toEqual([1]);']), at(2, 'expect([...h.state.shown]).toEqual'));
  assert.deepEqual(unpolled([head, '  expect(h.writes', '    .map((one) => one.kind)).toEqual([1]);']), at(2, 'expect( not closed on its line'));
  // A wait that passes at once, a helper that may click, and a hook are not taken as the host's answer.
  assert.deepEqual(unpolled([head, "  await page.click(); await expect(page.locator('#x-error')).not.toHaveText('boom'); expect(h.writes).toEqual([{}]);"]),
    at(2, 'expect(h.writes).toEqual'));
  assert.deepEqual(unpolled([head, '  await expect.poll(() => h.writes.length).toBe(1); await confirmPlan(page); expect(h.writes[1]).toEqual({});']),
    at(2, 'expect(h.writes[1]).toEqual'));
  assert.deepEqual(unpolled([head, '  await expect.poll(() => h.writes.length).toBe(1);', '});', 'test.afterEach(async () => {', '  expect(h.writes).toEqual([{}]);', '});']),
    [{ line: 5, title: '', read: 'expect(h.writes).toEqual' }]);
  // The reads of a request's query are not the route state.
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
